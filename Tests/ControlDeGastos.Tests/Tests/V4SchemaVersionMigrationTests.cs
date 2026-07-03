namespace ControlDeGastos.Tests.Tests;

public class V4SchemaVersionMigrationTests
{
    private readonly InMemoryStorageService _storage = new();

    public V4SchemaVersionMigrationTests()
    {
        _storage.ClearAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task MigrateAsync_GastosSinSchemaVersion_Asigna4()
    {
        var gastos = new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), Monto = 100, CategoriaId = Guid.NewGuid(), SchemaVersion = 0 },
            new() { Id = Guid.NewGuid(), Monto = 200, CategoriaId = Guid.NewGuid(), SchemaVersion = 0 },
        };
        await _storage.SetAsync("cdg_gastos", gastos);

        var migracion = new V4SchemaVersionMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(actualizados);
        Assert.All(actualizados!, g => Assert.Equal(4, g.SchemaVersion));
    }

    [Fact]
    public async Task MigrateAsync_PresupuestosSinSchemaVersion_Asigna4()
    {
        var presupuestos = new List<Presupuesto>
        {
            new() { Id = Guid.NewGuid(), MontoLimite = 1000, SchemaVersion = 0 },
        };
        await _storage.SetAsync("cdg_presupuestos", presupuestos);

        var migracion = new V4SchemaVersionMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Presupuesto>>("cdg_presupuestos");
        Assert.NotNull(actualizados);
        Assert.Equal(4, actualizados![0].SchemaVersion);
    }

    [Fact]
    public async Task MigrateAsync_RecurrenciasSinSchemaVersion_Asigna4()
    {
        var recurrencias = new List<Recurrencia>
        {
            new() { Id = Guid.NewGuid(), Monto = 500, SchemaVersion = 0 },
        };
        await _storage.SetAsync("cdg_recurrencias", recurrencias);

        var migracion = new V4SchemaVersionMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Recurrencia>>("cdg_recurrencias");
        Assert.NotNull(actualizados);
        Assert.Equal(4, actualizados![0].SchemaVersion);
    }

    [Fact]
    public async Task MigrateAsync_FinanciamientosSinSchemaVersion_Asigna4()
    {
        var financiamientos = new List<Financiamiento>
        {
            new() { Id = Guid.NewGuid(), MontoTotal = 10000, SchemaVersion = 0 },
        };
        await _storage.SetAsync("cdg_financiamientos", financiamientos);

        var migracion = new V4SchemaVersionMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Financiamiento>>("cdg_financiamientos");
        Assert.NotNull(actualizados);
        Assert.Equal(4, actualizados![0].SchemaVersion);
    }

    [Fact]
    public async Task MigrateAsync_CategoriasSinSchemaVersion_Asigna4()
    {
        var categorias = new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nombre = "Test", SchemaVersion = 0 },
        };
        await _storage.SetAsync("cdg_categorias", categorias);

        var migracion = new V4SchemaVersionMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Categoria>>("cdg_categorias");
        Assert.NotNull(actualizados);
        Assert.Equal(4, actualizados![0].SchemaVersion);
    }

    [Fact]
    public async Task MigrateAsync_UsuarioSinSchemaVersion_Asigna4()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Nombre = "Test", SchemaVersion = 0 };
        await _storage.SetAsync("cdg_usuario", usuario);

        var migracion = new V4SchemaVersionMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizado = await _storage.GetAsync<Usuario>("cdg_usuario");
        Assert.NotNull(actualizado);
        Assert.Equal(4, actualizado!.SchemaVersion);
    }

    [Fact]
    public async Task MigrateAsync_ProgresoRpgSinSchemaVersion_Asigna4()
    {
        var progreso = new ProgresoRPG { Id = Guid.NewGuid(), UsuarioId = Guid.NewGuid(), SchemaVersion = 0 };
        await _storage.SetAsync("cdg_progreso_rpg", progreso);

        var migracion = new V4SchemaVersionMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizado = await _storage.GetAsync<ProgresoRPG>("cdg_progreso_rpg");
        Assert.NotNull(actualizado);
        Assert.Equal(4, actualizado!.SchemaVersion);
    }

    [Fact]
    public async Task MigrateAsync_LicenciaSinSchemaVersion_Asigna4()
    {
        var licencia = new Licencia { Token = "test", SchemaVersion = 0 };
        await _storage.SetAsync("cdg_licencia", licencia);

        var migracion = new V4SchemaVersionMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizado = await _storage.GetAsync<Licencia>("cdg_licencia");
        Assert.NotNull(actualizado);
        Assert.Equal(4, actualizado!.SchemaVersion);
    }

    [Fact]
    public async Task MigrateAsync_EntidadesConSchemaVersion_NoModifica()
    {
        var gastos = new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), Monto = 100, CategoriaId = Guid.NewGuid(), SchemaVersion = 4 },
        };
        await _storage.SetAsync("cdg_gastos", gastos);

        var presupuestos = new List<Presupuesto>
        {
            new() { Id = Guid.NewGuid(), MontoLimite = 1000, SchemaVersion = 4 },
        };
        await _storage.SetAsync("cdg_presupuestos", presupuestos);

        var usuario = new Usuario { Id = Guid.NewGuid(), Nombre = "Test", SchemaVersion = 4 };
        await _storage.SetAsync("cdg_usuario", usuario);

        var migracion = new V4SchemaVersionMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
    }

    [Fact]
    public async Task MigrateAsync_SinDatos_NoFalla()
    {
        var migracion = new V4SchemaVersionMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
    }

    [Fact]
    public async Task MigrateAsync_ListasVacias_NoFalla()
    {
        await _storage.SetAsync("cdg_gastos", new List<Gasto>());
        await _storage.SetAsync("cdg_presupuestos", new List<Presupuesto>());

        var migracion = new V4SchemaVersionMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
    }

    [Fact]
    public async Task VersionDescripcion_DevuelveTextoExplicativo()
    {
        var migracion = new V4SchemaVersionMigration();

        Assert.Equal(4, migracion.Version);
        Assert.Contains("SchemaVersion", migracion.Descripcion);
    }
}
