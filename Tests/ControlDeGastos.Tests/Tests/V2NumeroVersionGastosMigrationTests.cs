using ControlDeGastos.Services.DataMigration.Migrations;

namespace ControlDeGastos.Tests.Tests;

public class V2NumeroVersionGastosMigrationTests
{
    private readonly InMemoryStorageService _storage = new();

    public V2NumeroVersionGastosMigrationTests()
    {
        _storage.ClearAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task MigrateAsync_GastosSinVersion_AsignaNumeroVersion1()
    {
        var gastos = new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), Monto = 100, CategoriaId = Guid.NewGuid(), NumeroVersion = 0 },
            new() { Id = Guid.NewGuid(), Monto = 200, CategoriaId = Guid.NewGuid(), NumeroVersion = 0 },
        };
        await _storage.SetAsync("cdg_gastos", gastos);

        var migracion = new V2NumeroVersionGastosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(actualizados);
        Assert.All(actualizados!, g => Assert.Equal(1, g.NumeroVersion));
    }

    [Fact]
    public async Task MigrateAsync_GastosConVersion_NoModifica()
    {
        var gastos = new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), Monto = 100, CategoriaId = Guid.NewGuid(), NumeroVersion = 5 },
        };
        await _storage.SetAsync("cdg_gastos", gastos);

        var migracion = new V2NumeroVersionGastosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
        var actualizados = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(actualizados);
        Assert.Equal(5, actualizados![0].NumeroVersion);
    }

    [Fact]
    public async Task MigrateAsync_SinGastos_NoFalla()
    {
        var migracion = new V2NumeroVersionGastosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
    }

    [Fact]
    public async Task MigrateAsync_GastosNulos_NoFalla()
    {
        await _storage.SetAsync<List<Gasto>?>("cdg_gastos", null);

        var migracion = new V2NumeroVersionGastosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
    }

    [Fact]
    public async Task MigrateAsync_VersionDescripcion_DevuelveTextoExplicativo()
    {
        var migracion = new V2NumeroVersionGastosMigration();

        Assert.Equal(2, migracion.Version);
        Assert.Contains("NumeroVersion", migracion.Descripcion);
    }
}
