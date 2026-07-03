namespace ControlDeGastos.Tests.Tests;

public class V3NumeroVersionEntidadesMigrationTests
{
    private readonly InMemoryStorageService _storage = new();

    public V3NumeroVersionEntidadesMigrationTests()
    {
        _storage.ClearAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task MigrateAsync_PresupuestosSinVersion_AsignaNumeroVersion1()
    {
        var presupuestos = new List<Presupuesto>
        {
            new() { Id = Guid.NewGuid(), MontoLimite = 1000, NumeroVersion = 0 },
            new() { Id = Guid.NewGuid(), MontoLimite = 2000, NumeroVersion = 0 },
        };
        await _storage.SetAsync("cdg_presupuestos", presupuestos);

        var migracion = new V3NumeroVersionEntidadesMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Presupuesto>>("cdg_presupuestos");
        Assert.NotNull(actualizados);
        Assert.All(actualizados!, p => Assert.Equal(1, p.NumeroVersion));
    }

    [Fact]
    public async Task MigrateAsync_RecurrenciasSinVersion_AsignaNumeroVersion1()
    {
        var recurrencias = new List<Recurrencia>
        {
            new() { Id = Guid.NewGuid(), Monto = 500, NumeroVersion = 0 },
        };
        await _storage.SetAsync("cdg_recurrencias", recurrencias);

        var migracion = new V3NumeroVersionEntidadesMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Recurrencia>>("cdg_recurrencias");
        Assert.NotNull(actualizados);
        Assert.Equal(1, actualizados![0].NumeroVersion);
    }

    [Fact]
    public async Task MigrateAsync_FinanciamientosSinVersion_AsignaNumeroVersion1()
    {
        var financiamientos = new List<Financiamiento>
        {
            new() { Id = Guid.NewGuid(), MontoTotal = 10000, NumeroVersion = 0 },
        };
        await _storage.SetAsync("cdg_financiamientos", financiamientos);

        var migracion = new V3NumeroVersionEntidadesMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Financiamiento>>("cdg_financiamientos");
        Assert.NotNull(actualizados);
        Assert.Equal(1, actualizados![0].NumeroVersion);
    }

    [Fact]
    public async Task MigrateAsync_CategoriasSinVersion_AsignaNumeroVersion1()
    {
        var categorias = new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nombre = "Test", NumeroVersion = 0 },
        };
        await _storage.SetAsync("cdg_categorias", categorias);

        var migracion = new V3NumeroVersionEntidadesMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Categoria>>("cdg_categorias");
        Assert.NotNull(actualizados);
        Assert.Equal(1, actualizados![0].NumeroVersion);
    }

    [Fact]
    public async Task MigrateAsync_UsuarioSinVersion_AsignaNumeroVersion1()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Nombre = "Test", NumeroVersion = 0 };
        await _storage.SetAsync("cdg_usuario", usuario);

        var migracion = new V3NumeroVersionEntidadesMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizado = await _storage.GetAsync<Usuario>("cdg_usuario");
        Assert.NotNull(actualizado);
        Assert.Equal(1, actualizado!.NumeroVersion);
    }

    [Fact]
    public async Task MigrateAsync_EntidadesConVersion_NoModifica()
    {
        var presupuestos = new List<Presupuesto>
        {
            new() { Id = Guid.NewGuid(), MontoLimite = 1000, NumeroVersion = 5 },
        };
        await _storage.SetAsync("cdg_presupuestos", presupuestos);

        var recurrencias = new List<Recurrencia>
        {
            new() { Id = Guid.NewGuid(), Monto = 500, NumeroVersion = 5 },
        };
        await _storage.SetAsync("cdg_recurrencias", recurrencias);

        var migracion = new V3NumeroVersionEntidadesMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
        var actualizados = await _storage.GetAsync<List<Presupuesto>>("cdg_presupuestos");
        Assert.NotNull(actualizados);
        Assert.Equal(5, actualizados![0].NumeroVersion);
    }

    [Fact]
    public async Task MigrateAsync_SinDatos_NoFalla()
    {
        var migracion = new V3NumeroVersionEntidadesMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
    }

    [Fact]
    public async Task MigrateAsync_ListasVacias_NoFalla()
    {
        await _storage.SetAsync("cdg_presupuestos", new List<Presupuesto>());
        await _storage.SetAsync("cdg_recurrencias", new List<Recurrencia>());

        var migracion = new V3NumeroVersionEntidadesMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
    }

    [Fact]
    public async Task VersionDescripcion_DevuelveTextoExplicativo()
    {
        var migracion = new V3NumeroVersionEntidadesMigration();

        Assert.Equal(3, migracion.Version);
        Assert.Contains("NumeroVersion", migracion.Descripcion);
    }
}
