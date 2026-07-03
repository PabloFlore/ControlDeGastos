using ControlDeGastos.Services.DataMigration;
using ControlDeGastos.Services.DataMigration.Migrations;

namespace ControlDeGastos.Tests.Tests;

public class DataMigrationRunnerTests
{
    private readonly InMemoryStorageService _storage = new();

    public DataMigrationRunnerTests()
    {
        _storage.ClearAsync().GetAwaiter().GetResult();
    }

    private DataMigrationRunner CrearRunner(params IDataMigration[] migraciones)
    {
        return new DataMigrationRunner(_storage, migraciones);
    }

    [Fact]
    public async Task EjecutarMigrationsAsync_SinVersion_CorreV1YV2()
    {
        Assert.False(await _storage.KeyExistsAsync("cdg_data_version"));

        var runner = CrearRunner(new V1SeedMigration(), new V2NumeroVersionGastosMigration());
        var result = await runner.EjecutarMigrationsAsync();

        var version = await _storage.GetAsync<int>("cdg_data_version");
        Assert.Equal(2, version);
        Assert.True(result.Exito);
        Assert.Equal(2, result.Ejecutadas.Count);
    }

    [Fact]
    public async Task EjecutarMigrationsAsync_DesdeVersion0_CorreV1YV2()
    {
        await _storage.SetAsync("cdg_data_version", 0);

        var runner = CrearRunner(new V1SeedMigration(), new V2NumeroVersionGastosMigration());
        var result = await runner.EjecutarMigrationsAsync();

        var version = await _storage.GetAsync<int>("cdg_data_version");
        Assert.Equal(2, version);
        Assert.True(result.Exito);
        Assert.Equal(2, result.Ejecutadas.Count);
    }

    [Fact]
    public async Task EjecutarMigrationsAsync_DesdeVersion1_CorreSoloV2()
    {
        await _storage.SetAsync("cdg_data_version", 1);

        var runner = CrearRunner(new V1SeedMigration(), new V2NumeroVersionGastosMigration());
        var result = await runner.EjecutarMigrationsAsync();

        var version = await _storage.GetAsync<int>("cdg_data_version");
        Assert.Equal(2, version);
        Assert.True(result.Exito);
        Assert.Single(result.Ejecutadas);
        Assert.Equal(2, result.Ejecutadas[0]);
    }

    [Fact]
    public async Task EjecutarMigrationsAsync_DesdeVersion2_NoCorreNada()
    {
        await _storage.SetAsync("cdg_data_version", 2);

        var runner = CrearRunner(new V1SeedMigration(), new V2NumeroVersionGastosMigration());
        var result = await runner.EjecutarMigrationsAsync();

        var version = await _storage.GetAsync<int>("cdg_data_version");
        Assert.Equal(2, version);
        Assert.True(result.Exito);
        Assert.Empty(result.Ejecutadas);
    }

    [Fact]
    public async Task EjecutarMigrationsAsync_DesdeVersion0_VersionActualizadaAlFinal()
    {
        var runner = CrearRunner(new V1SeedMigration(), new V2NumeroVersionGastosMigration());

        var versionAntes = await _storage.GetAsync<int>("cdg_data_version");
        Assert.Equal(0, versionAntes);

        var result = await runner.EjecutarMigrationsAsync();

        var versionDespues = await _storage.GetAsync<int>("cdg_data_version");
        Assert.Equal(2, versionDespues);
        Assert.True(result.Exito);
    }

    [Fact]
    public async Task EjecutarMigrationsAsync_SinMigracionesRegistradas_NoFalla()
    {
        var runner = CrearRunner();
        var result = await runner.EjecutarMigrationsAsync();

        Assert.True(result.Exito);
        Assert.Empty(result.Ejecutadas);
        Assert.False(await _storage.KeyExistsAsync("cdg_data_version"));
    }

    [Fact]
    public async Task EjecutarMigrationsAsync_SiMigracionFalla_NoActualizaVersion()
    {
        await _storage.SetAsync("cdg_data_version", 0);

        var falla = new FakeFailingMigration(version: 1);
        var runner = CrearRunner(falla, new V2NumeroVersionGastosMigration());

        var result = await runner.EjecutarMigrationsAsync();

        Assert.False(result.Exito);
        Assert.Equal(1, result.FallidaVersion);
        Assert.NotNull(result.Error);

        var version = await _storage.GetAsync<int>("cdg_data_version");
        Assert.Equal(0, version);
    }

    [Fact]
    public async Task MigrationResult_ToString_ConMigraciones_DevuelveResumen()
    {
        var runner = CrearRunner(new V1SeedMigration());
        var result = await runner.EjecutarMigrationsAsync();

        var texto = result.ToString();
        Assert.Contains("V1", texto);
        Assert.Contains("sin cambios", texto);
    }

    [Fact]
    public async Task MigrationResult_ToString_SinMigraciones_DevuelveMensaje()
    {
        var runner = CrearRunner();
        var result = await runner.EjecutarMigrationsAsync();

        Assert.Equal("No hay migraciones pendientes.", result.ToString());
    }

    [Fact]
    public async Task EjecutarMigrationsAsync_ConV3_CorreHastaV3()
    {
        await _storage.SetAsync("cdg_data_version", 2);

        var presupuestos = new List<Presupuesto>
        {
            new() { Id = Guid.NewGuid(), MontoLimite = 1000, NumeroVersion = 0 },
        };
        await _storage.SetAsync("cdg_presupuestos", presupuestos);

        var runner = CrearRunner(
            new V1SeedMigration(),
            new V2NumeroVersionGastosMigration(),
            new V3NumeroVersionEntidadesMigration());
        var result = await runner.EjecutarMigrationsAsync();

        var version = await _storage.GetAsync<int>("cdg_data_version");
        Assert.Equal(3, version);
        Assert.True(result.Exito);
        Assert.Single(result.Ejecutadas);
        Assert.Equal(3, result.Ejecutadas[0]);

        var actualizados = await _storage.GetAsync<List<Presupuesto>>("cdg_presupuestos");
        Assert.NotNull(actualizados);
        Assert.Equal(1, actualizados![0].NumeroVersion);
    }

    [Fact]
    public async Task EjecutarMigrationsAsync_ConV4_CorreHastaV4()
    {
        await _storage.SetAsync("cdg_data_version", 3);

        var gastos = new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), Monto = 100, CategoriaId = Guid.NewGuid(), SchemaVersion = 0 },
        };
        await _storage.SetAsync("cdg_gastos", gastos);

        var runner = CrearRunner(
            new V1SeedMigration(),
            new V2NumeroVersionGastosMigration(),
            new V3NumeroVersionEntidadesMigration(),
            new V4SchemaVersionMigration());
        var result = await runner.EjecutarMigrationsAsync();

        var version = await _storage.GetAsync<int>("cdg_data_version");
        Assert.Equal(4, version);
        Assert.True(result.Exito);
        Assert.Single(result.Ejecutadas);
        Assert.Equal(4, result.Ejecutadas[0]);

        var actualizados = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(actualizados);
        Assert.Equal(4, actualizados![0].SchemaVersion);
    }

    [Fact]
    public async Task EjecutarMigrationsAsync_FlujoCompletoV1AV4_ResultadosCorrectos()
    {
        var presupuestos = new List<Presupuesto>
        {
            new() { Id = Guid.NewGuid(), MontoLimite = 1000, NumeroVersion = 0, SchemaVersion = 0 },
        };
        var gastos = new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), Monto = 100, CategoriaId = Guid.NewGuid(), NumeroVersion = 0, SchemaVersion = 0 },
        };
        await _storage.SetAsync("cdg_presupuestos", presupuestos);
        await _storage.SetAsync("cdg_gastos", gastos);

        var runner = CrearRunner(
            new V1SeedMigration(),
            new V2NumeroVersionGastosMigration(),
            new V3NumeroVersionEntidadesMigration(),
            new V4SchemaVersionMigration());
        var result = await runner.EjecutarMigrationsAsync();

        Assert.True(result.Exito);

        var version = await _storage.GetAsync<int>("cdg_data_version");
        Assert.Equal(4, version);

        var presupuestosActualizados = await _storage.GetAsync<List<Presupuesto>>("cdg_presupuestos");
        Assert.NotNull(presupuestosActualizados);
        Assert.Equal(1, presupuestosActualizados![0].NumeroVersion);
        Assert.Equal(4, presupuestosActualizados[0].SchemaVersion);

        var gastosActualizados = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(gastosActualizados);
        Assert.Equal(1, gastosActualizados![0].NumeroVersion);
        Assert.Equal(4, gastosActualizados[0].SchemaVersion);
    }

    [Fact]
    public async Task MigrationResult_ToString_ConError_DevuelveError()
    {
        var falla = new FakeFailingMigration(version: 99);
        var runner = CrearRunner(falla);
        await _storage.SetAsync("cdg_data_version", 0);

        var result = await runner.EjecutarMigrationsAsync();

        Assert.Contains("V99", result.ToString());
        Assert.Contains("fallida", result.ToString());
    }

    [Fact]
    public async Task ObtenerEstadoAsync_CuandoAlDia_NoTienePendientes()
    {
        await _storage.SetAsync("cdg_data_version", DataMigrationRunner.VersionActual);

        var runner = CrearRunner(new V1SeedMigration(), new V2NumeroVersionGastosMigration());
        var estado = await runner.ObtenerEstadoAsync();

        Assert.Equal(DataMigrationRunner.VersionActual, estado.VersionActual);
        Assert.Equal(DataMigrationRunner.VersionActual, estado.VersionEsperada);
        Assert.True(estado.AlDia);
        Assert.Empty(estado.MigracionesPendientes);
    }

    [Fact]
    public async Task ObtenerEstadoAsync_CuandoHayPendientes_LasLista()
    {
        await _storage.SetAsync("cdg_data_version", 0);

        var runner = CrearRunner(new V1SeedMigration(), new V2NumeroVersionGastosMigration());
        var estado = await runner.ObtenerEstadoAsync();

        Assert.Equal(0, estado.VersionActual);
        Assert.False(estado.AlDia);
        Assert.Equal(2, estado.MigracionesDisponibles.Count);
        Assert.Equal(2, estado.MigracionesPendientes.Count);
        Assert.Equal(1, estado.MigracionesPendientes[0].Version);
        Assert.Equal(2, estado.MigracionesPendientes[1].Version);
    }

    [Fact]
    public async Task RepararAsync_ReEjecutaTodasLasMigraciones()
    {
        var presupuesto = new Presupuesto { Id = Guid.NewGuid(), MontoLimite = 1000, NumeroVersion = 0 };
        await _storage.SetAsync("cdg_presupuestos", new List<Presupuesto> { presupuesto });
        await _storage.SetAsync("cdg_data_version", DataMigrationRunner.VersionActual);

        var runner = CrearRunner(
            new V1SeedMigration(),
            new V2NumeroVersionGastosMigration(),
            new V3NumeroVersionEntidadesMigration());

        var resultado = await runner.RepararAsync();

        Assert.True(resultado.Exito);
        Assert.Equal(3, resultado.Ejecutadas.Count);

        var version = await _storage.GetAsync<int>("cdg_data_version");
        Assert.Equal(3, version);

        var presupuestos = await _storage.GetAsync<List<Presupuesto>>("cdg_presupuestos");
        Assert.NotNull(presupuestos);
        Assert.Equal(1, presupuestos![0].NumeroVersion);
    }

    [Fact]
    public async Task ObtenerEstadoAsync_MigracionesDisponiblesTienenDescripcion()
    {
        var runner = CrearRunner(
            new V1SeedMigration(),
            new V2NumeroVersionGastosMigration());
        var estado = await runner.ObtenerEstadoAsync();

        Assert.All(estado.MigracionesDisponibles, m =>
        {
            Assert.NotEmpty(m.Descripcion);
            Assert.True(m.Version > 0);
        });
    }

    [Fact]
    public async Task RepararAsync_CuandoFalla_RetornaError()
    {
        await _storage.SetAsync("cdg_data_version", 0);

        var falla = new FakeFailingMigration(version: 1);
        var runner = CrearRunner(falla);

        var resultado = await runner.RepararAsync();

        Assert.False(resultado.Exito);
        Assert.Equal(1, resultado.FallidaVersion);
    }

    private class FakeFailingMigration : IDataMigration
    {
        public int Version { get; }
        public string Descripcion => "Failing migration for testing";
        public FakeFailingMigration(int version) => Version = version;
        public Task<bool> MigrateAsync(IStorageService storage)
            => throw new InvalidOperationException("Error simulado en migración");
    }
}
