namespace ControlDeGastos.Tests.Tests;

public class V5LimpiarReferenciasHuerfanasMigrationTests
{
    private readonly InMemoryStorageService _storage = new();

    public V5LimpiarReferenciasHuerfanasMigrationTests()
    {
        _storage.ClearAsync().GetAwaiter().GetResult();
    }

    private readonly Guid _catValida = Guid.NewGuid();
    private readonly Guid _catValida2 = Guid.NewGuid();
    private readonly Guid _recurrenciaValida = Guid.NewGuid();
    private readonly Guid _financiamientoValido = Guid.NewGuid();

    private async Task SetupDatosValidosAsync()
    {
        await _storage.SetAsync("cdg_categorias", new List<Categoria>
        {
            new() { Id = _catValida, Nombre = "Comida" },
            new() { Id = _catValida2, Nombre = "Transporte" },
        });
        await _storage.SetAsync("cdg_recurrencias", new List<Recurrencia>
        {
            new() { Id = _recurrenciaValida, Monto = 100, TipoRecurrencia = TipoRecurrencia.Mensual },
        });
        await _storage.SetAsync("cdg_financiamientos", new List<Financiamiento>
        {
            new() { Id = _financiamientoValido, MontoTotal = 5000, Tipo = "Credito" },
        });
    }

    [Fact]
    public async Task MigrateAsync_GastoConCategoriaInexistente_EliminaGasto()
    {
        await SetupDatosValidosAsync();

        var gastos = new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), Monto = 100, CategoriaId = Guid.NewGuid(), Descripcion = "Huerfano" },
            new() { Id = Guid.NewGuid(), Monto = 200, CategoriaId = _catValida, Descripcion = "Valido" },
        };
        await _storage.SetAsync("cdg_gastos", gastos);

        var migracion = new V5LimpiarReferenciasHuerfanasMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(actualizados);
        Assert.Single(actualizados!);
        Assert.Equal("Valido", actualizados![0].Descripcion);
    }

    [Fact]
    public async Task MigrateAsync_GastoConRecurrenciaInexistente_LimpiaReferencia()
    {
        await SetupDatosValidosAsync();

        var gastos = new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), Monto = 100, CategoriaId = _catValida, RecurrenciaId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), Monto = 200, CategoriaId = _catValida, RecurrenciaId = _recurrenciaValida },
        };
        await _storage.SetAsync("cdg_gastos", gastos);

        var migracion = new V5LimpiarReferenciasHuerfanasMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(actualizados);
        Assert.Equal(2, actualizados!.Count);
        Assert.Null(actualizados[0].RecurrenciaId);
        Assert.Equal(_recurrenciaValida, actualizados[1].RecurrenciaId);
    }

    [Fact]
    public async Task MigrateAsync_GastoConFinanciamientoInexistente_LimpiaReferencia()
    {
        await SetupDatosValidosAsync();

        var gastos = new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), Monto = 100, CategoriaId = _catValida, FinanciamientoId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), Monto = 200, CategoriaId = _catValida, FinanciamientoId = _financiamientoValido },
        };
        await _storage.SetAsync("cdg_gastos", gastos);

        var migracion = new V5LimpiarReferenciasHuerfanasMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(actualizados);
        Assert.Equal(2, actualizados!.Count);
        Assert.Null(actualizados[0].FinanciamientoId);
        Assert.Equal(_financiamientoValido, actualizados[1].FinanciamientoId);
    }

    [Fact]
    public async Task MigrateAsync_PresupuestoConCategoriaInexistente_EliminaPresupuesto()
    {
        await SetupDatosValidosAsync();

        var presupuestos = new List<Presupuesto>
        {
            new() { Id = Guid.NewGuid(), MontoLimite = 1000, CategoriaId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), MontoLimite = 2000, CategoriaId = _catValida },
        };
        await _storage.SetAsync("cdg_presupuestos", presupuestos);

        var migracion = new V5LimpiarReferenciasHuerfanasMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Presupuesto>>("cdg_presupuestos");
        Assert.NotNull(actualizados);
        Assert.Single(actualizados!);
        Assert.Equal(_catValida, actualizados![0].CategoriaId);
    }

    [Fact]
    public async Task MigrateAsync_PresupuestoConCategoriaNull_SeConserva()
    {
        await SetupDatosValidosAsync();

        var presupuestos = new List<Presupuesto>
        {
            new() { Id = Guid.NewGuid(), MontoLimite = 1000, CategoriaId = null },
        };
        await _storage.SetAsync("cdg_presupuestos", presupuestos);

        var migracion = new V5LimpiarReferenciasHuerfanasMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
        var actualizados = await _storage.GetAsync<List<Presupuesto>>("cdg_presupuestos");
        Assert.NotNull(actualizados);
        Assert.Single(actualizados!);
        Assert.Null(actualizados![0].CategoriaId);
    }

    [Fact]
    public async Task MigrateAsync_TodoValido_NoModifica()
    {
        await SetupDatosValidosAsync();

        var gastos = new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), Monto = 100, CategoriaId = _catValida, RecurrenciaId = _recurrenciaValida },
        };
        await _storage.SetAsync("cdg_gastos", gastos);

        var presupuestos = new List<Presupuesto>
        {
            new() { Id = Guid.NewGuid(), MontoLimite = 1000, CategoriaId = _catValida2 },
        };
        await _storage.SetAsync("cdg_presupuestos", presupuestos);

        var migracion = new V5LimpiarReferenciasHuerfanasMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
    }

    [Fact]
    public async Task MigrateAsync_SinDatos_NoFalla()
    {
        var migracion = new V5LimpiarReferenciasHuerfanasMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
    }

    [Fact]
    public async Task MigrateAsync_ListasVacias_NoFalla()
    {
        await _storage.SetAsync("cdg_categorias", new List<Categoria>());
        await _storage.SetAsync("cdg_gastos", new List<Gasto>());
        await _storage.SetAsync("cdg_presupuestos", new List<Presupuesto>());

        var migracion = new V5LimpiarReferenciasHuerfanasMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.False(modifico);
    }

    [Fact]
    public async Task MigrateAsync_GastosHuerfanosYPresupuestosHuerfanos_AmbosLimpios()
    {
        await SetupDatosValidosAsync();

        var gastos = new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), Monto = 100, CategoriaId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), Monto = 200, CategoriaId = _catValida },
        };
        await _storage.SetAsync("cdg_gastos", gastos);

        var presupuestos = new List<Presupuesto>
        {
            new() { Id = Guid.NewGuid(), MontoLimite = 1000, CategoriaId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), MontoLimite = 2000, CategoriaId = _catValida },
        };
        await _storage.SetAsync("cdg_presupuestos", presupuestos);

        var migracion = new V5LimpiarReferenciasHuerfanasMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);

        var gastosActualizados = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(gastosActualizados);
        Assert.Single(gastosActualizados!);

        var presupuestosActualizados = await _storage.GetAsync<List<Presupuesto>>("cdg_presupuestos");
        Assert.NotNull(presupuestosActualizados);
        Assert.Single(presupuestosActualizados!);
    }

    [Fact]
    public async Task MigrateAsync_RecurrenciaYFinanciamientoHuerfanosEnMismoGasto_AmbosLimpios()
    {
        await SetupDatosValidosAsync();

        var gastos = new List<Gasto>
        {
            new()
            {
                Id = Guid.NewGuid(), Monto = 100, CategoriaId = _catValida,
                RecurrenciaId = Guid.NewGuid(), FinanciamientoId = Guid.NewGuid(),
            },
        };
        await _storage.SetAsync("cdg_gastos", gastos);

        var migracion = new V5LimpiarReferenciasHuerfanasMigration();
        var modifico = await migracion.MigrateAsync(_storage);

        Assert.True(modifico);
        var actualizados = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(actualizados);
        Assert.Single(actualizados!);
        Assert.Null(actualizados![0].RecurrenciaId);
        Assert.Null(actualizados[0].FinanciamientoId);
    }

    [Fact]
    public async Task VersionDescripcion_DevuelveTextoExplicativo()
    {
        var migracion = new V5LimpiarReferenciasHuerfanasMigration();

        Assert.Equal(5, migracion.Version);
        Assert.Contains("categorias", migracion.Descripcion);
    }
}
