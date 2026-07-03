using Microsoft.Extensions.Logging;

namespace ControlDeGastos.Tests.Tests;

public class FinanciamientoServiceTests
{
    private readonly InMemoryStorageService _storage = new();
    private readonly Mock<IUsuarioService> _usuarioServiceMock = new();
    private readonly Mock<ISupabaseService> _supabaseMock = new();
    private readonly Mock<IGastoService> _gastoServiceMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<ISyncService> _syncMock = new();
    private readonly Guid _usuarioId = Guid.NewGuid();

    public FinanciamientoServiceTests()
    {
        _serviceProviderMock
            .Setup(s => s.GetService(typeof(ISyncService)))
            .Returns(_syncMock.Object);

        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Local });
        _storage.ClearAsync().GetAwaiter().GetResult();
    }

    private FinanciamientoService CrearService()
        => new(_storage, _usuarioServiceMock.Object, _supabaseMock.Object, _gastoServiceMock.Object, _serviceProviderMock.Object, new Mock<ILogger<FinanciamientoService>>().Object);

    [Fact]
    public async Task ObtenerFinanciamientosAsync_SinDatos_RetornaListaVacia()
    {
        var service = CrearService();
        var result = await service.ObtenerFinanciamientosAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task ObtenerFinanciamientosAsync_ConDatos_FiltraPorUsuario()
    {
        var otroId = Guid.NewGuid();
        var catId = Guid.NewGuid();
        await _storage.SetAsync("cdg_financiamientos", new List<Financiamiento>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, MontoTotal = 5000, Banco = "BBVA", Alias = "Mi TC", Tipo = "Credito", CategoriaId = catId },
            new() { Id = Guid.NewGuid(), UsuarioId = otroId, MontoTotal = 10000, Banco = "Banorte", Alias = "Otra", Tipo = "Credito", CategoriaId = catId },
        });

        var service = CrearService();
        var result = await service.ObtenerFinanciamientosAsync();

        Assert.Single(result);
        Assert.Equal(5000, result[0].MontoTotal);
    }

    [Fact]
    public async Task ObtenerFinanciamientosAsync_ConHogar_FiltraPorHogar()
    {
        var catId = Guid.NewGuid();
        var hogarId = "hogar-test";
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Local, HogarId = hogarId });

        await _storage.SetAsync("cdg_financiamientos", new List<Financiamiento>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, MontoTotal = 5000, Banco = "BBVA", Alias = "Mi TC", Tipo = "Credito", HogarId = hogarId, CategoriaId = catId },
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, MontoTotal = 3000, Banco = "HSBC", Alias = "Otro hogar", Tipo = "Credito", HogarId = "otro-hogar", CategoriaId = catId },
        });

        var service = CrearService();
        var result = await service.ObtenerFinanciamientosAsync();

        Assert.Single(result);
        Assert.Equal("Mi TC", result[0].Alias);
    }

    [Fact]
    public async Task CrearFinanciamientoAsync_GuardaEnStorage_Y_AsignaUsuario()
    {
        var service = CrearService();
        var catId = Guid.NewGuid();
        var item = new Financiamiento
        {
            MontoTotal = 10000,
            PlazoMeses = 12,
            Banco = "BBVA",
            Alias = "TC Test",
            Tipo = "Credito",
            CategoriaId = catId,
        };

        var creado = await service.CrearFinanciamientoAsync(item);

        Assert.NotNull(creado);
        Assert.Equal(_usuarioId, creado.UsuarioId);
        Assert.False(creado.Sincronizado);

        var todos = await service.ObtenerFinanciamientosAsync();
        Assert.Single(todos);
        Assert.Equal(10000, todos[0].MontoTotal);
    }

    [Fact]
    public async Task CrearFinanciamientoAsync_ConHogar_AsignaHogarId()
    {
        var hogarId = "hogar-test";
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Local, HogarId = hogarId });

        var service = CrearService();
        var item = new Financiamiento
        {
            MontoTotal = 5000,
            PlazoMeses = 6,
            Banco = "Banorte",
            Alias = "TC Hogar",
            Tipo = "Credito",
            CategoriaId = Guid.NewGuid(),
        };

        var creado = await service.CrearFinanciamientoAsync(item);

        Assert.Equal(hogarId, creado.HogarId);
    }

    [Fact]
    public async Task CrearFinanciamientoAsync_PlanNube_Sincroniza()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _supabaseMock
            .Setup(s => s.GuardarAsync("financiamientos", It.IsAny<Financiamiento>()))
            .ReturnsAsync((string _, Financiamiento f) => f);

        var service = CrearService();
        var item = new Financiamiento
        {
            MontoTotal = 15000,
            PlazoMeses = 24,
            Banco = "HSBC",
            Alias = "TC Nube",
            Tipo = "Credito",
            CategoriaId = Guid.NewGuid(),
        };

        var creado = await service.CrearFinanciamientoAsync(item);

        _supabaseMock.Verify(s => s.GuardarAsync("financiamientos", It.IsAny<Financiamiento>()), Times.Once);
        Assert.True(creado.Sincronizado);
    }

    [Fact]
    public async Task CrearFinanciamientoAsync_CreaGastoAutomatico()
    {
        Gasto? gastoCreado = null;
        _gastoServiceMock
            .Setup(s => s.CrearGastoAsync(It.IsAny<Gasto>()))
            .Callback<Gasto>(g => gastoCreado = g)
            .ReturnsAsync((Gasto g) => g);

        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        var service = CrearService();
        var catId = Guid.NewGuid();
        var item = new Financiamiento
        {
            MontoTotal = 120000,
            PlazoMeses = 12,
            TasaInteresAnual = 0,
            Banco = "BBVA",
            Alias = "TC Gasto Auto",
            Tipo = "Credito",
            CategoriaId = catId,
        };

        await service.CrearFinanciamientoAsync(item);

        Assert.NotNull(gastoCreado);
        Assert.Equal(catId, gastoCreado.CategoriaId);
        Assert.Equal(10000m, gastoCreado.Monto); // 120000 / 12
        Assert.Contains("Credito", gastoCreado.Descripcion);
        Assert.Contains("TC Gasto Auto", gastoCreado.Descripcion);
        Assert.Equal(item.Id, gastoCreado.FinanciamientoId);
    }

    [Fact]
    public async Task ActualizarFinanciamientoAsync_ActualizaEnStorage()
    {
        var service = CrearService();
        var catId = Guid.NewGuid();
        var item = new Financiamiento
        {
            MontoTotal = 5000,
            PlazoMeses = 6,
            Banco = "BBVA",
            Alias = "Original",
            Tipo = "Credito",
            CategoriaId = catId,
        };

        var creado = await service.CrearFinanciamientoAsync(item);
        creado.Alias = "Actualizado";
        creado.MontoTotal = 9999;

        var actualizado = await service.ActualizarFinanciamientoAsync(creado);

        Assert.Equal("Actualizado", actualizado.Alias);
        Assert.Equal(9999, actualizado.MontoTotal);

        var todos = await service.ObtenerFinanciamientosAsync();
        Assert.Single(todos);
        Assert.Equal("Actualizado", todos[0].Alias);
    }

    [Fact]
    public async Task ActualizarFinanciamientoAsync_PlanNube_Sincroniza()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _supabaseMock
            .Setup(s => s.ActualizarAsync("financiamientos", It.IsAny<Guid>(), It.IsAny<Financiamiento>()))
            .ReturnsAsync((string _, Guid _, Financiamiento f) => f);

        var service = CrearService();
        var catId = Guid.NewGuid();
        var item = new Financiamiento
        {
            MontoTotal = 5000,
            PlazoMeses = 6,
            Banco = "BBVA",
            Alias = "Original",
            Tipo = "Credito",
            CategoriaId = catId,
        };

        var creado = await service.CrearFinanciamientoAsync(item);

        _supabaseMock.Invocations.Clear();

        creado.Alias = "Sincronizado";
        await service.ActualizarFinanciamientoAsync(creado);

        _supabaseMock.Verify(s => s.ActualizarAsync("financiamientos", creado.Id, It.IsAny<Financiamiento>()), Times.Once);
    }

    [Fact]
    public async Task EliminarFinanciamientoAsync_RemueveDeStorage()
    {
        var service = CrearService();
        var catId = Guid.NewGuid();
        var item = new Financiamiento
        {
            MontoTotal = 3000,
            PlazoMeses = 3,
            Banco = "Banorte",
            Alias = "A eliminar",
            Tipo = "Credito",
            CategoriaId = catId,
        };

        var creado = await service.CrearFinanciamientoAsync(item);
        Assert.Single(await service.ObtenerFinanciamientosAsync());

        await service.EliminarFinanciamientoAsync(creado.Id);
        Assert.Empty(await service.ObtenerFinanciamientosAsync());
    }

    [Fact]
    public async Task EliminarFinanciamientoAsync_PlanNube_EliminaEnSupabase()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        var service = CrearService();
        var catId = Guid.NewGuid();
        var item = new Financiamiento
        {
            MontoTotal = 3000,
            PlazoMeses = 3,
            Banco = "Banorte",
            Alias = "A eliminar",
            Tipo = "Credito",
            CategoriaId = catId,
        };

        var creado = await service.CrearFinanciamientoAsync(item);
        await service.EliminarFinanciamientoAsync(creado.Id);

        _supabaseMock.Verify(s => s.EliminarAsync<Financiamiento>("financiamientos", creado.Id), Times.Once);
    }

    [Fact]
    public async Task EliminarFinanciamientoConGastosAsync_EliminaFinanciamientoYSusGastos()
    {
        var service = CrearService();
        var catId = Guid.NewGuid();
        var item = new Financiamiento
        {
            MontoTotal = 6000,
            PlazoMeses = 6,
            Banco = "HSBC",
            Alias = "Cascada",
            Tipo = "Credito",
            CategoriaId = catId,
        };

        var creado = await service.CrearFinanciamientoAsync(item);

        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>
            {
                new() { Id = Guid.NewGuid(), FinanciamientoId = creado.Id, Monto = 1000, CategoriaId = catId },
                new() { Id = Guid.NewGuid(), FinanciamientoId = creado.Id, Monto = 2000, CategoriaId = catId },
                new() { Id = Guid.NewGuid(), Monto = 500, CategoriaId = catId },
            });

        await service.EliminarFinanciamientoConGastosAsync(creado.Id);

        _gastoServiceMock.Verify(s => s.EliminarGastoAsync(It.IsAny<Guid>()), Times.Exactly(2));
        Assert.Empty(await service.ObtenerFinanciamientosAsync());
    }

    [Fact]
    public async Task ObtenerBancosAsync_RetornaPredefinidos()
    {
        var service = CrearService();
        var bancos = await service.ObtenerBancosAsync();

        Assert.NotEmpty(bancos);
        Assert.Contains("BBVA", bancos);
        Assert.Contains("Banorte", bancos);
        Assert.Contains("Nu Bank", bancos);
    }

    [Fact]
    public async Task ObtenerBancosAsync_ConPersonalizados_IncluyeAmbos()
    {
        await _storage.SetAsync("cdg_bancos_personalizados", new List<string> { "Mi Banco", "Otro Banco" });

        var service = CrearService();
        var bancos = await service.ObtenerBancosAsync();

        Assert.Contains("BBVA", bancos);
        Assert.Contains("Mi Banco", bancos);
        Assert.Contains("Otro Banco", bancos);
    }

    [Fact]
    public async Task AgregarBancoPersonalizadoAsync_AgregaNuevo()
    {
        var service = CrearService();
        await service.AgregarBancoPersonalizadoAsync("Banco Test");
        var bancos = await service.ObtenerBancosAsync();

        Assert.Contains("Banco Test", bancos);
    }

    [Fact]
    public async Task AgregarBancoPersonalizadoAsync_Vacio_NoAgrega()
    {
        var service = CrearService();
        await service.AgregarBancoPersonalizadoAsync("");
        await service.AgregarBancoPersonalizadoAsync("  ");

        var bancos = await service.ObtenerBancosAsync();
        Assert.DoesNotContain("", bancos);
    }

    [Fact]
    public async Task AgregarBancoPersonalizadoAsync_Duplicado_NoAgrega()
    {
        var service = CrearService();
        await service.AgregarBancoPersonalizadoAsync("Banco Test");
        await service.AgregarBancoPersonalizadoAsync("Banco Test");

        var bancos = await service.ObtenerBancosAsync();
        Assert.Single(bancos.Where(b => b == "Banco Test"));
    }

    [Fact]
    public async Task AgregarBancoPersonalizadoAsync_Predefinido_NoAgrega()
    {
        var service = CrearService();
        await service.AgregarBancoPersonalizadoAsync("BBVA");

        var bancos = await service.ObtenerBancosAsync();
        Assert.Single(bancos.Where(b => b == "BBVA"));
    }

    [Fact]
    public void CalcularPagoAmortizado_SinInteres_DivideMontoTotal()
    {
        var resultado = FinanciamientoService.CalcularPagoAmortizado(120000, 12, null);
        Assert.Equal(10000m, resultado);
    }

    [Fact]
    public void CalcularPagoAmortizado_ConInteres_CalculaCorrectamente()
    {
        var resultado = FinanciamientoService.CalcularPagoAmortizado(10000, 12, 12m);
        Assert.True(resultado > 0);
        Assert.True(Math.Abs(resultado - 888.49m) < 1m);
    }

    [Fact]
    public void CalcularPagoAmortizado_PlazoCero_RetornaMontoTotal()
    {
        var resultado = FinanciamientoService.CalcularPagoAmortizado(5000, 0, null);
        Assert.Equal(5000m, resultado);
    }

    [Fact]
    public void CalcularPagoAmortizado_InteresCero_DivideMontoTotal()
    {
        var resultado = FinanciamientoService.CalcularPagoAmortizado(6000, 6, 0m);
        Assert.Equal(1000m, resultado);
    }

    [Fact]
    public async Task CrearFinanciamientoAsync_ConTasaInteres_GastoCalculaPagoAmortizado()
    {
        Gasto? gastoCreado = null;
        _gastoServiceMock
            .Setup(s => s.CrearGastoAsync(It.IsAny<Gasto>()))
            .Callback<Gasto>(g => gastoCreado = g)
            .ReturnsAsync((Gasto g) => g);

        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        var service = CrearService();
        var item = new Financiamiento
        {
            MontoTotal = 10000,
            PlazoMeses = 12,
            TasaInteresAnual = 12m,
            Banco = "BBVA",
            Alias = "TC Interes",
            Tipo = "Credito",
            CategoriaId = Guid.NewGuid(),
        };

        await service.CrearFinanciamientoAsync(item);

        Assert.NotNull(gastoCreado);
        Assert.True(gastoCreado.Monto > 833m); // mayor que pago sin interés (10000/12 ≈ 833)
        Assert.True(gastoCreado.Monto < 1000m);
    }

    [Fact]
    public async Task CrearFinanciamientoAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _supabaseMock
            .Setup(s => s.GuardarAsync("financiamientos", It.IsAny<Financiamiento>()))
            .ThrowsAsync(new Exception("Error de red"));

        _supabaseMock
            .Setup(s => s.ActualizarAsync("financiamientos", It.IsAny<Guid>(), It.IsAny<Financiamiento>()))
            .ThrowsAsync(new Exception("Error de red al actualizar"));

        _gastoServiceMock
            .Setup(s => s.CrearGastoAsync(It.IsAny<Gasto>()))
            .ReturnsAsync((Gasto g) => g);

        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        var service = CrearService();
        var item = new Financiamiento
        {
            MontoTotal = 10000, PlazoMeses = 12, Banco = "BBVA",
            Alias = "TC", Tipo = "Credito", CategoriaId = Guid.NewGuid(),
        };

        var ex = await Record.ExceptionAsync(() => service.CrearFinanciamientoAsync(item));
        Assert.Null(ex);

        var todos = await service.ObtenerFinanciamientosAsync();
        Assert.Single(todos);
        Assert.False(todos[0].Sincronizado);
    }

    [Fact]
    public async Task ActualizarFinanciamientoAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _supabaseMock
            .Setup(s => s.GuardarAsync("financiamientos", It.IsAny<Financiamiento>()))
            .ReturnsAsync((string _, Financiamiento f) => f);

        _supabaseMock
            .Setup(s => s.ActualizarAsync("financiamientos", It.IsAny<Guid>(), It.IsAny<Financiamiento>()))
            .ThrowsAsync(new Exception("Error de red"));

        _gastoServiceMock
            .Setup(s => s.CrearGastoAsync(It.IsAny<Gasto>()))
            .ReturnsAsync((Gasto g) => g);

        var service = CrearService();
        var item = new Financiamiento
        {
            MontoTotal = 5000, PlazoMeses = 6, Banco = "BBVA",
            Alias = "Original", Tipo = "Credito", CategoriaId = Guid.NewGuid(),
        };
        var creado = await service.CrearFinanciamientoAsync(item);

        _supabaseMock.Invocations.Clear();
        creado.Alias = "Actualizado";
        var ex = await Record.ExceptionAsync(() => service.ActualizarFinanciamientoAsync(creado));
        Assert.Null(ex);

        var todos = await service.ObtenerFinanciamientosAsync();
        Assert.Equal("Actualizado", todos[0].Alias);
    }

    [Fact]
    public async Task ActualizarFinanciamientoAsync_PlanLocal_SincronizadoFalse()
    {
        var service = CrearService();
        var item = new Financiamiento
        {
            MontoTotal = 5000, PlazoMeses = 6, Banco = "BBVA",
            Alias = "Original", Tipo = "Credito", CategoriaId = Guid.NewGuid(),
        };
        var creado = await service.CrearFinanciamientoAsync(item);

        creado.Alias = "Local";
        var actualizado = await service.ActualizarFinanciamientoAsync(creado);

        Assert.False(actualizado.Sincronizado);
    }

    [Fact]
    public async Task EliminarFinanciamientoAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _supabaseMock
            .Setup(s => s.EliminarAsync<Financiamiento>("financiamientos", It.IsAny<Guid>()))
            .ThrowsAsync(new Exception("Error de red"));

        _supabaseMock
            .Setup(s => s.GuardarAsync("financiamientos", It.IsAny<Financiamiento>()))
            .ReturnsAsync((string _, Financiamiento f) => f);

        _gastoServiceMock
            .Setup(s => s.CrearGastoAsync(It.IsAny<Gasto>()))
            .ReturnsAsync((Gasto g) => g);

        var service = CrearService();
        var item = new Financiamiento
        {
            MontoTotal = 3000, PlazoMeses = 3, Banco = "Banorte",
            Alias = "A eliminar", Tipo = "Credito", CategoriaId = Guid.NewGuid(),
        };
        var creado = await service.CrearFinanciamientoAsync(item);

        var ex = await Record.ExceptionAsync(() => service.EliminarFinanciamientoAsync(creado.Id));
        Assert.Null(ex);
        Assert.Empty(await service.ObtenerFinanciamientosAsync());
    }

    [Fact]
    public async Task CrearFinanciamientoAsync_GastoServiceFalla_NoLanzaExcepcion()
    {
        _gastoServiceMock
            .Setup(s => s.CrearGastoAsync(It.IsAny<Gasto>()))
            .ThrowsAsync(new Exception("Error al crear gasto"));

        var service = CrearService();
        var item = new Financiamiento
        {
            MontoTotal = 10000, PlazoMeses = 12, Banco = "BBVA",
            Alias = "TC", Tipo = "Credito", CategoriaId = Guid.NewGuid(),
        };

        var ex = await Record.ExceptionAsync(() => service.CrearFinanciamientoAsync(item));
        Assert.Null(ex);

        var todos = await service.ObtenerFinanciamientosAsync();
        Assert.Single(todos);
    }
}
