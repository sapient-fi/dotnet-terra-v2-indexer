namespace Sapient.Kernel.Config;

public interface IDbConfig
{
    public string ConnectionString { get; }

    public bool DisableMigrationsDuringBoot { get; }
}