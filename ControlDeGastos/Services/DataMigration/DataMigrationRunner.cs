namespace ControlDeGastos.Services.DataMigration;

public class DataMigrationRunner : IDataMigrationRunner
{
    private const string VersionKey = "cdg_data_version";
    private readonly IStorageService _storage;
    private readonly IEnumerable<IDataMigration> _migrations;

    public static int VersionActual => 8;
    public IEnumerable<IDataMigration> MigracionesDisponibles => _migrations;

    public DataMigrationRunner(IStorageService storage, IEnumerable<IDataMigration> migrations)
    {
        _storage = storage;
        _migrations = migrations;
    }

    public async Task<MigrationResult> EjecutarMigrationsAsync()
    {
        var version = await ObtenerVersionStorageAsync();
        var pendientes = _migrations
            .Where(m => m.Version > version)
            .OrderBy(m => m.Version)
            .ToList();

        var resultado = new MigrationResult();

        foreach (var migracion in pendientes)
        {
            try
            {
                var modifico = await migracion.MigrateAsync(_storage);
                resultado.Ejecutadas.Add(migracion.Version);
                if (modifico)
                    resultado.ConCambios.Add(migracion.Version);

                await ValidarMigracionAsync(migracion);
            }
            catch (Exception ex)
            {
                resultado.FallidaVersion = migracion.Version;
                resultado.Error = ex.Message;
                return resultado;
            }
        }

        if (pendientes.Count > 0)
        {
            await _storage.SetAsync(VersionKey, pendientes.Max(m => m.Version));
        }

        return resultado;
    }

    public async Task<MigrationState> ObtenerEstadoAsync()
    {
        var version = await ObtenerVersionStorageAsync();
        var ordenadas = _migrations.OrderBy(m => m.Version).ToList();

        return new MigrationState
        {
            VersionActual = version,
            VersionEsperada = VersionActual,
            MigracionesDisponibles = ordenadas.Select(m => new MigrationInfo
            {
                Version = m.Version,
                Descripcion = m.Descripcion,
                Pendiente = m.Version > version,
            }).ToList(),
            MigracionesPendientes = ordenadas
                .Where(m => m.Version > version)
                .Select(m => new MigrationInfo
                {
                    Version = m.Version,
                    Descripcion = m.Descripcion,
                    Pendiente = true,
                }).ToList(),
        };
    }

    public async Task<MigrationResult> RepararAsync()
    {
        await _storage.SetAsync(VersionKey, 0);
        var resultado = await EjecutarMigrationsAsync();
        return resultado;
    }

    private async Task<int> ObtenerVersionStorageAsync()
    {
        return await _storage.GetAsync<int>(VersionKey);
    }

    private async Task ValidarMigracionAsync(IDataMigration migracion)
    {
        try
        {
            await _storage.GetAsync<object>(VersionKey);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Post-migration validation failed after V{migracion.Version}: storage unavailable. {ex.Message}", ex);
        }
    }
}
