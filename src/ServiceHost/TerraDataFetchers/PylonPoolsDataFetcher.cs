using NewRelic.Api.Agent;
using Pylonboard.ServiceHost.DAL.TerraMoney;
using Pylonboard.ServiceHost.TerraDataFetchers.Internal.PylonPools;

namespace Pylonboard.ServiceHost.TerraDataFetchers;

public class PylonPoolsDataFether
{
    private readonly ILogger<PylonPoolsDataFether> _logger;
    private readonly LowLevelPoolFetcher _poolFetcher;

    public PylonPoolsDataFether(
        ILogger<PylonPoolsDataFether> logger,
        LowLevelPoolFetcher poolFetcher
    )
    {
        _logger = logger;
        _poolFetcher = poolFetcher;
    }

    [Transaction]
    public async Task FetchDataAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Pylon Pool data fetcher commencing");
            
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.WHITE_WHALE_1, TerraPylonPoolFriendlyName.WhiteWhale1, stoppingToken);
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.WHITE_WHALE_2, TerraPylonPoolFriendlyName.WhiteWhale2, stoppingToken);
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.WHITE_WHALE_3, TerraPylonPoolFriendlyName.WhiteWhale3, stoppingToken);
            
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.LOOP_1, TerraPylonPoolFriendlyName.Loop1, stoppingToken);
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.LOOP_2, TerraPylonPoolFriendlyName.Loop2, stoppingToken);
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.LOOP_3, TerraPylonPoolFriendlyName.Loop3, stoppingToken);

        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.ORION, TerraPylonPoolFriendlyName.Orion, stoppingToken);
            
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.VKR_1, TerraPylonPoolFriendlyName.Valkyrie1, stoppingToken);
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.VKR_2, TerraPylonPoolFriendlyName.Valkyrie2, stoppingToken);
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.VKR_3, TerraPylonPoolFriendlyName.Valkyrie3, stoppingToken);
            
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.TERRA_WORLD_1, TerraPylonPoolFriendlyName.TerraWorld1, stoppingToken);
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.TERRA_WORLD_2, TerraPylonPoolFriendlyName.TerraWorld2, stoppingToken);
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.TERRA_WORLD_3, TerraPylonPoolFriendlyName.TerraWorld3, stoppingToken);
            
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.MINE_1, TerraPylonPoolFriendlyName.Mine1, stoppingToken);
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.MINE_2, TerraPylonPoolFriendlyName.Mine2, stoppingToken);
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.MINE_3, TerraPylonPoolFriendlyName.Mine3, stoppingToken);
        
        await _poolFetcher.FetchPoolDataAsync(TerraPylonGatewayContracts.NEXUS, TerraPylonPoolFriendlyName.Nexus, stoppingToken);
    }
}