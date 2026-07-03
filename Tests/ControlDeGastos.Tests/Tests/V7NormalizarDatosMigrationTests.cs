namespace ControlDeGastos.Tests.Tests;

public class V7NormalizarDatosMigrationTests
{
    private readonly InMemoryStorageService _storage = new();

    public V7NormalizarDatosMigrationTests()
    {
        _storage.ClearAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task MigrateAsync_GastoConIdVacio_EliminaGasto()
    {
        await _storage.SetAsync("cdg_gastos", new List<Gasto>
        {
            new() { Id = Guid.Empty, Monto = 100 },
            new() { Id = Guid.NewGuid(), Monto = 200 },
        });

        var migracion = new V7NormalizarDatosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var gastos = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(gastos);
        Assert.Single(gastos!);
    }

    [Fact]
    public async Task MigrateAsync_GastoConDescripcionNull_CorrigeAStringVacio()
    {
        var id = Guid.NewGuid();
        await _storage.SetAsync("cdg_gastos", new List<Gasto>
        {
            new() { Id = id, Monto = 100, Descripcion = null, CategoriaId = Guid.NewGuid() },
        });

        var migracion = new V7NormalizarDatosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var gastos = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(gastos);
        Assert.Equal(string.Empty, gastos![0].Descripcion);
    }

    [Fact]
    public async Task MigrateAsync_CategoriaConNombreNull_AsignaSinNombre()
    {
        await _storage.SetAsync("cdg_categorias", new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nombre = null! },
        });

        var migracion = new V7NormalizarDatosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var cats = await _storage.GetAsync<List<Categoria>>("cdg_categorias");
        Assert.NotNull(cats);
        Assert.Equal("Sin nombre", cats![0].Nombre);
    }

    [Fact]
    public async Task MigrateAsync_CategoriaConIconoNull_AsignaDefault()
    {
        await _storage.SetAsync("cdg_categorias", new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nombre = "Test", Icono = null! },
        });

        var migracion = new V7NormalizarDatosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var cats = await _storage.GetAsync<List<Categoria>>("cdg_categorias");
        Assert.NotNull(cats);
        Assert.Equal("\U0001f4c1", cats![0].Icono);
    }

    [Fact]
    public async Task MigrateAsync_PresupuestoConMontoCero_CorrigeA1()
    {
        await _storage.SetAsync("cdg_presupuestos", new List<Presupuesto>
        {
            new() { Id = Guid.NewGuid(), MontoLimite = 0 },
        });

        var migracion = new V7NormalizarDatosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var pres = await _storage.GetAsync<List<Presupuesto>>("cdg_presupuestos");
        Assert.NotNull(pres);
        Assert.Equal(1, pres![0].MontoLimite);
    }

    [Fact]
    public async Task MigrateAsync_RecurrenciaConIntervaloCero_CorrigeA1()
    {
        await _storage.SetAsync("cdg_recurrencias", new List<Recurrencia>
        {
            new() { Id = Guid.NewGuid(), Monto = 100, Intervalo = 0, ProximaFecha = DateTime.UtcNow, TipoRecurrencia = TipoRecurrencia.Mensual },
        });

        var migracion = new V7NormalizarDatosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var recs = await _storage.GetAsync<List<Recurrencia>>("cdg_recurrencias");
        Assert.NotNull(recs);
        Assert.Equal(1, recs![0].Intervalo);
    }

    [Fact]
    public async Task MigrateAsync_FinanciamientoConBancoNull_CorrigeAStringVacio()
    {
        await _storage.SetAsync("cdg_financiamientos", new List<Financiamiento>
        {
            new() { Id = Guid.NewGuid(), MontoTotal = 5000, PlazoMeses = 12, Banco = null! },
        });

        var migracion = new V7NormalizarDatosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var fins = await _storage.GetAsync<List<Financiamiento>>("cdg_financiamientos");
        Assert.NotNull(fins);
        Assert.Equal(string.Empty, fins![0].Banco);
    }

    [Fact]
    public async Task MigrateAsync_ProgresoRpgConNivelCero_CorrigeA1()
    {
        await _storage.SetAsync("cdg_progreso_rpg", new ProgresoRPG
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            Nivel = 0,
            ExpRequerida = 0,
            HpMaximo = 0,
        });

        var migracion = new V7NormalizarDatosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var prog = await _storage.GetAsync<ProgresoRPG>("cdg_progreso_rpg");
        Assert.NotNull(prog);
        Assert.Equal(1, prog!.Nivel);
        Assert.Equal(100, prog.ExpRequerida);
        Assert.Equal(100, prog.HpMaximo);
    }

    [Fact]
    public async Task MigrateAsync_UsuarioConNombreNull_CorrigeAStringVacio()
    {
        await _storage.SetAsync("cdg_usuario", new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = null!,
        });

        var migracion = new V7NormalizarDatosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var user = await _storage.GetAsync<Usuario>("cdg_usuario");
        Assert.NotNull(user);
        Assert.Equal(string.Empty, user!.Nombre);
    }

    [Fact]
    public async Task MigrateAsync_UsuarioConMonedaNull_CorrigeAMXN()
    {
        await _storage.SetAsync("cdg_usuario", new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Test",
            Moneda = null!,
        });

        var migracion = new V7NormalizarDatosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var user = await _storage.GetAsync<Usuario>("cdg_usuario");
        Assert.NotNull(user);
        Assert.Equal("MXN", user!.Moneda);
    }

    [Fact]
    public async Task MigrateAsync_DatosCorrectos_NoModifica()
    {
        var catId = Guid.NewGuid();
        await _storage.SetAsync("cdg_gastos", new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), Monto = 100, CategoriaId = catId, Descripcion = "ok" },
        });
        await _storage.SetAsync("cdg_categorias", new List<Categoria>
        {
            new() { Id = catId, Nombre = "Ok", Icono = "\U0001f4c1", Color = "#000" },
        });

        var migracion = new V7NormalizarDatosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
    }

    [Fact]
    public async Task MigrateAsync_SinDatos_NoFalla()
    {
        var migracion = new V7NormalizarDatosMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
    }

    [Fact]
    public async Task VersionDescripcion_DevuelveTextoExplicativo()
    {
        var migracion = new V7NormalizarDatosMigration();

        Assert.Equal(7, migracion.Version);
        Assert.Contains("Normaliza", migracion.Descripcion);
    }
}
