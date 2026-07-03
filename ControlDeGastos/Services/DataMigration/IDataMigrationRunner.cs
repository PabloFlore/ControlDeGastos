namespace ControlDeGastos.Services.DataMigration;

public interface IDataMigrationRunner
{
    Task<MigrationResult> EjecutarMigrationsAsync();
    Task<MigrationState> ObtenerEstadoAsync();
    Task<MigrationResult> RepararAsync();
}
