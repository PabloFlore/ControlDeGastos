namespace ControlDeGastos.Services.DataMigration;

public interface IDataMigration
{
    int Version { get; }
    string Descripcion { get; }
    Task<bool> MigrateAsync(IStorageService storage);
}
