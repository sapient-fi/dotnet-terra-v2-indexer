using System.Data;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NewRelic.Api.Agent;
using Pylonboard.Kernel.DAL.Entities.Terra;
using Pylonboard.Kernel.IdGeneration;
using ServiceStack;
using ServiceStack.Data;
using ServiceStack.OrmLite;
using TerraDotnet;
using TerraDotnet.TerraFcd.Messages;
using TerraDotnet.TerraFcd.Messages.Wasm;
using TxLogExtensions = TerraDotnet.Extensions.TxLogExtensions;

namespace Pylonboard.Infrastructure.Hosting.TerraDataFetchers;

public class MineStakingDataFetcher
{
    private readonly ILogger<MineStakingDataFetcher> _logger;
    private readonly TerraTransactionEnumerator _transactionEnumerator;
    private readonly IdGenerator _idGenerator;
    private readonly IDbConnectionFactory _dbFactory;

    public MineStakingDataFetcher(
        ILogger<MineStakingDataFetcher> logger,
        TerraTransactionEnumerator transactionEnumerator,
        IdGenerator idGenerator,
        IDbConnectionFactory dbFactory
    )
    {
        _logger = logger;
        _transactionEnumerator = transactionEnumerator;
        _idGenerator = idGenerator;
        _dbFactory = dbFactory;
    }

    [Trace]
    public async Task FetchDataAsync(CancellationToken stoppingToken, bool fullResync = false)

    {
        _logger.LogInformation("Fetching fresh MINE staking transactions");
        using var db = _dbFactory.OpenDbConnection();

        var latestRow = await db.SingleAsync(
            db.From<TerraMineStakingEntity>()
                .OrderByDescending(q => q.CreatedAt), token: stoppingToken);

        await foreach (var (tx, msg) in _transactionEnumerator.EnumerateTransactionsAsync(
                           0,
                           100,
                           TerraStakingContracts.MINE_STAKING_CONTRACT,
                           stoppingToken))
        {
            if (await ProcessSingleTransactionAsync(tx, msg, latestRow, db, fullResync, stoppingToken))
            {
                break;
            }
        }

        _logger.LogInformation("Done inserting transactions");
    }

    /// <summary>
    /// Process a single Terra transaction from the blockchain
    /// </summary>
    /// <param name="tx"></param>
    /// <param name="msg"></param>
    /// <param name="latestRow"></param>
    /// <param name="db"></param>
    /// <param name="fullResync"></param>
    /// <param name="stoppingToken"></param>
    /// <returns>A boolean as to whether transaction enumeration should be STOPPED</returns>
    public async Task<bool> ProcessSingleTransactionAsync(TerraTxWrapper tx,
        CoreStdTx msg,
        TerraMineStakingEntity? latestRow,
        IDbConnection db,
        bool fullResync,
        CancellationToken stoppingToken
    )
    {
        if (fullResync)
        {
            var exists = await db.SingleAsync(
                db.From<TerraMineStakingEntity>()
                    .Where(q => q.TransactionId == tx.Id && q.CreatedAt == tx.CreatedAt)
                    .Take(1),
                token: stoppingToken
            );
            if (exists != default)
            {
                _logger.LogInformation(
                    "Transaction with id {TxId} and hash {TxHash} already exists, skipping during full resync",
                    tx.Id, tx.TransactionHash);
                return false;
            }
        }
        else if (tx.Id == latestRow?.TransactionId)
        {
            _logger.LogInformation("Transaction with id {TxId} and hash {TxHash} already exists, aborting", tx.Id,
                tx.TransactionHash);
            return true;
        }

        using var dbTx = db.BeginTransaction();
        await dbTx.Connection.SaveAsync(obj: new TerraRawTransactionEntity
            {
                Id = tx.Id,
                CreatedAt = tx.CreatedAt,
                TxHash = tx.TransactionHash,
                RawTx = JsonSerializer.Serialize(tx),
            },
            token: stoppingToken
        );

        foreach (var properMsg in msg.Messages.Select(innerMsg => innerMsg as WasmMsgExecuteContract))
        {
            if (properMsg == default)
            {
                _logger.LogWarning("Transaction with id {Id} did not have a message of type {Type}", tx.Id,
                    typeof(WasmMsgExecuteContract));
                continue;
            }

            var amount = 0m;
            if (properMsg.Value.ExecuteMessage.CastVote != null || properMsg.Value.ExecuteMessage.EndPoll != null)
            {
                _logger.LogDebug("Cast vote or end poll, ignoring");
                continue;
            }

            if (properMsg.Value.ExecuteMessage.Mint != null)
            {
                _logger.LogDebug("SPEC mint, ignoring");
                continue;
            }

            if (properMsg.Value.ExecuteMessage.Sweep != null || properMsg.Value.ExecuteMessage.Earn != null)
            {
                _logger.LogDebug("Pylon buy-back sweep, handled later");
                continue;
            }

            if (properMsg.Value.ExecuteMessage.UpdateConfig != null)
            {
                _logger.LogDebug("Contract config update, ignore");
                continue;
            }

            if (properMsg.Value.ExecuteMessage.Transfer != null)
            {
                _logger.LogDebug("Contact COL-4 transfer, ignore");
                continue;
            }

            if (properMsg.Value.ExecuteMessage.Send != null)
            {
                if (!properMsg.Value.ExecuteMessage.Send.Contract.EqualsIgnoreCase(TerraStakingContracts
                        .MINE_STAKING_CONTRACT))
                {
                    _logger.LogDebug("Send to contract that is not MINE governance, skipping ");
                    continue;
                }


                amount = Convert.ToDecimal(properMsg.Value.ExecuteMessage.Send.Amount) / 1_000_000m;
            }
            // SPEC farm does funky stuff
            else if (properMsg.Value.ExecuteMessage.Withdraw != null)
            {
                _logger.LogDebug("Spec farm withdraw, skip");
                continue;
                var contractAddresses = TxLogExtensions.QueryTxLogsForAttributes(tx.Logs, "from_contract",
                        attribute => StringExtensions.EqualsIgnoreCase(attribute.Key, "contract_address"))
                    .ToList();

                if (contractAddresses.Any(attribute =>
                        attribute.Value.EqualsIgnoreCase(TerraStakingContracts.SPEC_PYLON_FARM)))
                {
                    if (contractAddresses.Any(attribute =>
                            attribute.Value.EqualsIgnoreCase(TerraStakingContracts.MINE_STAKING_CONTRACT)))
                    {
                        var farmAmount =
                            TxLogExtensions.QueryTxLogsForAttributeFirstOrDefault(tx.Logs, "from_contract",
                                "farm_amount");
                        amount = farmAmount.Value.ToInt64() / 1_000_000m;
                    }
                }
            }
            // SPEC compounding provides tokens for the MINE governance contract
            else if (properMsg.Value.ExecuteMessage.Compound != null)
            {
                _logger.LogDebug("Spec farm compound, skip");
                continue;
                var contractAddresses = TxLogExtensions.QueryTxLogsForAttributes(tx.Logs, "from_contract",
                        attribute => StringExtensions.EqualsIgnoreCase(attribute.Key, "contract_address"))
                    .ToList();

                if (contractAddresses.Any(attribute =>
                        attribute.Value.EqualsIgnoreCase(TerraStakingContracts.SPEC_PYLON_FARM)))
                {
                    if (contractAddresses.Any(attribute =>
                            attribute.Value.EqualsIgnoreCase(TerraStakingContracts.MINE_STAKING_CONTRACT)))
                    {
                        var stakeAmount =
                            TxLogExtensions.QueryTxLogsForAttributeFirstOrDefault(tx.Logs, "from_contract",
                                "stake_amount");
                        amount = stakeAmount.Value.ToInt64() / 1_000_000m;
                    }
                }
            }
            else if (properMsg.Value.ExecuteMessage.Airdrop?.Claim != null)
            {
                _logger.LogDebug("Airdrop claim, skipping");
                continue;
            }
            else if (properMsg.Value.ExecuteMessage.WithdrawVotingTokens != null)
            {
                amount = -1 *
                         (Convert.ToDecimal(properMsg.Value.ExecuteMessage.WithdrawVotingTokens
                             .Amount) / 1_000_000m);
            }
            else if (properMsg.Value.ExecuteMessage.Staking?.Unstake != null)
            {
                amount = -1 * properMsg.Value.ExecuteMessage.Staking.Unstake.Amount.ToInt64() / 1_000_000m;
            }

            else
            {
                _logger.LogWarning(
                    "Transaction w. id {Id} and hash {TxHash} does not have send nor withdraw amount",
                    tx.Id,
                    tx.TransactionHash
                );
                // throw new NotImplementedException("Cannot handle this TX for some reason");
                continue;
            }

            var data = new TerraMineStakingEntity
            {
                Id = _idGenerator.Snowflake(),
                TransactionId = tx.Id,
                CreatedAt = tx.CreatedAt.UtcDateTime,
                Sender = properMsg.Value.Sender,
                Amount = amount,
                TxHash = tx.TransactionHash,
                IsBuyBack = false,
            };
            await dbTx.Connection.SaveAsync(data, token: stoppingToken);
        }

        dbTx.Commit();
        return false;
    }
}