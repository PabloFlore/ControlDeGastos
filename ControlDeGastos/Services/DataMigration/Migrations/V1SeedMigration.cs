using ControlDeGastos.Services.DataMigration;

namespace ControlDeGastos.Services.DataMigration.Migrations;

public class V1SeedMigration : IDataMigration
{
    public int Version => 1;
    public string Descripcion => "Migración semilla inicial (sin cambios estructurales)";

    public Task<bool> MigrateAsync(IStorageService storage)
    {
        return Task.FromResult(false);
    }
}
