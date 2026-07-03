using Microsoft.Extensions.Logging;

namespace ControlDeGastos.Tests.Tests;

public class GastoServiceTests
{
    private readonly InMemoryStorageService _storage = new();
    private readonly Mock<IUsuarioService> _usuarioServiceMock = new();
    private readonly Mock<IGamificacionService> _gamificacionMock = new();
    private readonly Mock<IPresupuestoService> _presupuestoMock = new();

    private readonly Mock<ILicenciaService> _licenciaMock = new();
    private readonly Mock<ISupabaseService> _supabaseMock = new();
    private readonly Mock<ISyncService> _syncMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Guid _usuarioId = Guid.NewGuid();

    public GastoServiceTests()
    {
        _serviceProviderMock
            .Setup(s => s.GetService(typeof(IPresupuestoService)))
            .Returns(_presupuestoMock.Object);
        _serviceProviderMock
            .Setup(s => s.GetService(typeof(IGamificacionService)))
            .Returns(_gamificacionMock.Object);
        _serviceProviderMock
            .Setup(s => s.GetService(typeof(ISyncService)))
            .Returns(_syncMock.Object);

        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Local, ModoGamificadoActivo = false });
        _licenciaMock
            .Setup(s => s.VerificarYActualizarVigenciaAsync())
            .ReturnsAsync(true);
        _presupuestoMock
            .Setup(s => s.ObtenerPresupuestosAsync())
            .ReturnsAsync(new List<Presupuesto>());

        _storage.ClearAsync().GetAwaiter().GetResult();
    }

    private GastoService CrearService()
        => new(_storage, _usuarioServiceMock.Object,
               _licenciaMock.Object, _supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<GastoService>>().Object);

    [Fact]
    public async Task CrearGastoAsync_GuardaEnStorage()
    {
        var service = CrearService();
        var gasto = new Gasto { Monto = 500, CategoriaId = Guid.NewGuid() };

        var creado = await service.CrearGastoAsync(gasto);

        Assert.NotNull(creado);
        Assert.Equal(_usuarioId, creado.UsuarioId);
        Assert.False(creado.Sincronizado);

        var todos = await service.ObtenerGastosAsync();
        Assert.Single(todos);
        Assert.Equal(500, todos[0].Monto);
    }

    [Fact]
    public async Task CrearGastoAsync_ConGamificacion_DisparaService()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, ModoGamificadoActivo = true });

        var service = CrearService();
        var gasto = new Gasto { Monto = 300, CategoriaId = Guid.NewGuid() };

        await service.CrearGastoAsync(gasto);

        _gamificacionMock.Verify(
            s => s.AplicarGastoAsync(It.IsAny<Gasto>(), It.IsAny<decimal>(), It.IsAny<decimal>()),
            Times.Once);
    }

    [Fact]
    public async Task CrearGastoAsync_ConPlanNube_Sincroniza()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _supabaseMock
            .Setup(s => s.GuardarAsync("gastos", It.IsAny<Gasto>()))
            .ReturnsAsync((string _, Gasto g) => g);

        var service = CrearService();
        var gasto = new Gasto { Monto = 200, CategoriaId = Guid.NewGuid() };

        var creado = await service.CrearGastoAsync(gasto);

        _supabaseMock.Verify(s => s.GuardarAsync("gastos", It.IsAny<Gasto>()), Times.Once);
        Assert.True(creado.Sincronizado);
    }

    [Fact]
    public async Task CrearGastoAsync_SinGamificacion_NoDispara()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, ModoGamificadoActivo = false });

        var service = CrearService();
        var gasto = new Gasto { Monto = 100, CategoriaId = Guid.NewGuid() };

        await service.CrearGastoAsync(gasto);

        _gamificacionMock.Verify(
            s => s.AplicarGastoAsync(It.IsAny<Gasto>(), It.IsAny<decimal>(), It.IsAny<decimal>()),
            Times.Never);
    }

    [Fact]
    public async Task ObtenerGastosAsync_FiltraPorUsuario()
    {
        var otroId = Guid.NewGuid();

        await _storage.SetAsync("cdg_gastos", new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 100, CategoriaId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), UsuarioId = otroId, Monto = 200, CategoriaId = Guid.NewGuid() },
        });

        var service = CrearService();
        var gastos = await service.ObtenerGastosAsync();

        Assert.Single(gastos);
        Assert.Equal(100, gastos[0].Monto);
    }

    [Fact]
    public async Task ObtenerGastosPorMesAsync_FiltraPorMes()
    {
        await _storage.ClearAsync();

        await _storage.SetAsync("cdg_gastos", new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 100,
                Fecha = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc), CategoriaId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 200,
                Fecha = new DateTime(2024, 7, 15, 12, 0, 0, DateTimeKind.Utc), CategoriaId = Guid.NewGuid() },
        });

        var service = CrearService();
        var gastos = await service.ObtenerGastosPorMesAsync(2024, 6);

        Assert.Single(gastos);
        Assert.Equal(100, gastos[0].Monto);
    }

    [Fact]
    public async Task ActualizarGastoAsync_ActualizaEnStorage()
    {
        var service = CrearService();
        var gasto = new Gasto { Monto = 100, CategoriaId = Guid.NewGuid() };

        var creado = await service.CrearGastoAsync(gasto);
        creado.Monto = 999;
        creado.Descripcion = "Actualizado";

        await service.ActualizarGastoAsync(creado);

        var todos = await service.ObtenerGastosAsync();
        Assert.Single(todos);
        Assert.Equal(999, todos[0].Monto);
        Assert.Equal("Actualizado", todos[0].Descripcion);
    }

    [Fact]
    public async Task EliminarGastoAsync_RemueveDeStorage()
    {
        var service = CrearService();
        var gasto = new Gasto { Monto = 50, CategoriaId = Guid.NewGuid() };

        var creado = await service.CrearGastoAsync(gasto);
        Assert.Single(await service.ObtenerGastosAsync());

        await service.EliminarGastoAsync(creado.Id);
        Assert.Empty(await service.ObtenerGastosAsync());
    }

    [Fact]
    public async Task MigrarGastosAHogarAsync_AsignaHogarId()
    {
        var storage = new InMemoryStorageService();
        var spMock = new Mock<IServiceProvider>();
        spMock.Setup(s => s.GetService(typeof(IPresupuestoService))).Returns(_presupuestoMock.Object);
        spMock.Setup(s => s.GetService(typeof(IGamificacionService))).Returns(_gamificacionMock.Object);
        spMock.Setup(s => s.GetService(typeof(ISyncService))).Returns(_syncMock.Object);
        var service = new GastoService(
            storage, _usuarioServiceMock.Object,
            _licenciaMock.Object, _supabaseMock.Object, spMock.Object, new Mock<ILogger<GastoService>>().Object);

        var hogarId = "hogar-test-123";
        var catId = Guid.NewGuid();

        await storage.SetAsync("cdg_gastos", new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 100, CategoriaId = catId },
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 200, CategoriaId = catId, HogarId = "otro-hogar" },
        });

        await service.MigrarGastosAHogarAsync(hogarId);

        var todos = await service.ObtenerGastosAsync();
        Assert.Equal(2, todos.Count);
        Assert.Equal(hogarId, todos.First(g => g.HogarId == hogarId).HogarId);
        Assert.Equal("otro-hogar", todos.First(g => g.HogarId == "otro-hogar").HogarId);
    }

    [Fact]
    public async Task ObtenerGastosPorRangoAsync_FiltraPorRango()
    {
        await _storage.ClearAsync();

        await _storage.SetAsync("cdg_gastos", new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 100,
                Fecha = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc), CategoriaId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 200,
                Fecha = new DateTime(2024, 6, 15, 12, 0, 0, DateTimeKind.Utc), CategoriaId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 300,
                Fecha = new DateTime(2024, 7, 1, 0, 0, 0, DateTimeKind.Utc), CategoriaId = Guid.NewGuid() },
        });

        var service = CrearService();
        var desde = new DateTime(2024, 6, 5, 0, 0, 0, DateTimeKind.Utc);
        var hasta = new DateTime(2024, 7, 31, 0, 0, 0, DateTimeKind.Utc);

        var gastos = await service.ObtenerGastosPorRangoAsync(desde, hasta);

        Assert.Equal(2, gastos.Count);
        Assert.Contains(gastos, g => g.Monto == 200);
        Assert.Contains(gastos, g => g.Monto == 300);
    }

    [Fact]
    public async Task ObtenerGastosPorRangoAsync_SinResultados()
    {
        await _storage.ClearAsync();

        await _storage.SetAsync("cdg_gastos", new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 100,
                Fecha = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc), CategoriaId = Guid.NewGuid() },
        });

        var service = CrearService();
        var gastos = await service.ObtenerGastosPorRangoAsync(
            new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 6, 30, 0, 0, 0, DateTimeKind.Utc));

        Assert.Empty(gastos);
    }

    [Fact]
    public async Task ObtenerGastosPorRangoAsync_RangoCompleto()
    {
        await _storage.ClearAsync();

        await _storage.SetAsync("cdg_gastos", new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 100,
                Fecha = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc), CategoriaId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 200,
                Fecha = new DateTime(2024, 6, 30, 12, 0, 0, DateTimeKind.Utc), CategoriaId = Guid.NewGuid() },
        });

        var service = CrearService();
        var gastos = await service.ObtenerGastosPorRangoAsync(
            new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 6, 30, 23, 59, 59, DateTimeKind.Utc));

        Assert.Equal(2, gastos.Count);
    }

    [Fact]
    public async Task MarcarTodosPendientesSyncAsync_MarcaTodosComoNoSincronizados()
    {
        await _storage.SetAsync("cdg_gastos", new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 100, Sincronizado = true, CategoriaId = Guid.NewGuid() },
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 200, Sincronizado = true, CategoriaId = Guid.NewGuid() },
        });

        var service = CrearService();
        await service.MarcarTodosPendientesSyncAsync();

        var gastos = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(gastos);
        Assert.All(gastos!, g => Assert.False(g.Sincronizado));
    }

    [Fact]
    public async Task ActualizarGastoAsync_ConflictoRemotoGana_LanzaExcepcion()
    {
        var gastoId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        var remoto = new Gasto
        {
            Id = gastoId,
            Monto = 500,
            CategoriaId = catId,
            UsuarioId = _usuarioId,
            CreadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ActualizadoEn = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Gasto> { remoto });

        _supabaseMock
            .Setup(s => s.ActualizarAsync("gastos", It.IsAny<Guid>(), It.IsAny<Gasto>()))
            .ReturnsAsync((string _, Guid _, Gasto g) => g);

        var local = new Gasto
        {
            Id = gastoId,
            Monto = 100,
            CategoriaId = catId,
            UsuarioId = _usuarioId,
            CreadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ActualizadoEn = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        };

        await _storage.SetAsync("cdg_gastos", new List<Gasto> { local });

        var service = CrearService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ActualizarGastoAsync(local));
    }

    [Fact]
    public async Task ObtenerGastosAsync_ConHogar_FiltraPorHogar()
    {
        var hogarId = "hogar-test";
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, HogarId = hogarId });

        var catId = Guid.NewGuid();
        await _storage.SetAsync("cdg_gastos", new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 100, CategoriaId = catId, HogarId = hogarId },
            new() { Id = Guid.NewGuid(), UsuarioId = _usuarioId, Monto = 200, CategoriaId = catId, HogarId = "otro-hogar" },
        });

        var service = CrearService();
        var gastos = await service.ObtenerGastosAsync();

        Assert.Single(gastos);
        Assert.Equal(100, gastos[0].Monto);
    }

    [Fact]
    public async Task CrearGastoAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _supabaseMock
            .Setup(s => s.GuardarAsync("gastos", It.IsAny<Gasto>()))
            .ThrowsAsync(new Exception("Error de red"));

        var service = CrearService();
        var gasto = new Gasto { Monto = 500, CategoriaId = Guid.NewGuid() };

        var ex = await Record.ExceptionAsync(() => service.CrearGastoAsync(gasto));
        Assert.Null(ex);
    }

    [Fact]
    public async Task ActualizarGastoAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new Exception("Error de red"));

        var service = CrearService();
        var gasto = new Gasto { Monto = 100, CategoriaId = Guid.NewGuid() };
        var creado = await service.CrearGastoAsync(gasto);

        creado.Monto = 999;
        var ex = await Record.ExceptionAsync(() => service.ActualizarGastoAsync(creado));
        Assert.Null(ex);
    }

    [Fact]
    public async Task ActualizarGastoAsync_PlanLocal_EstableceSincronizadoFalse()
    {
        var service = CrearService();
        var gasto = new Gasto { Monto = 100, CategoriaId = Guid.NewGuid() };
        var creado = await service.CrearGastoAsync(gasto);

        creado.Monto = 999;
        var actualizado = await service.ActualizarGastoAsync(creado);

        Assert.False(actualizado.Sincronizado);
    }

    [Fact]
    public async Task EliminarGastoAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _supabaseMock
            .Setup(s => s.EliminarAsync<Gasto>("gastos", It.IsAny<Guid>()))
            .ThrowsAsync(new Exception("Error de red"));

        var service = CrearService();
        var gasto = new Gasto { Monto = 50, CategoriaId = Guid.NewGuid() };
        var creado = await service.CrearGastoAsync(gasto);

        var ex = await Record.ExceptionAsync(() => service.EliminarGastoAsync(creado.Id));
        Assert.Null(ex);
        Assert.Empty(await service.ObtenerGastosAsync());
    }

    [Fact]
    public async Task ActualizarGastoAsync_PlanNube_SinRemoto_ActualizaLocal()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Gasto>());

        _supabaseMock
            .Setup(s => s.ActualizarAsync("gastos", It.IsAny<Guid>(), It.IsAny<Gasto>()))
            .ReturnsAsync((string _, object _, Gasto g) => g);

        var service = CrearService();
        var gasto = new Gasto { Monto = 100, CategoriaId = Guid.NewGuid() };
        var creado = await service.CrearGastoAsync(gasto);

        creado.Monto = 999;
        var actualizado = await service.ActualizarGastoAsync(creado);

        Assert.True(actualizado.Sincronizado);
        Assert.Equal(999, actualizado.Monto);
        _supabaseMock.Verify(s => s.ActualizarAsync("gastos", creado.Id, It.IsAny<Gasto>()), Times.Once);
    }

    [Fact]
    public async Task ActualizarGastoAsync_PlanNube_LocalMasRecienteQueRemoto_Actualiza()
    {
        var gastoId = Guid.NewGuid();
        var catId = Guid.NewGuid();

        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        var remoto = new Gasto
        {
            Id = gastoId, Monto = 500, CategoriaId = catId, UsuarioId = _usuarioId,
            CreadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ActualizadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Gasto> { remoto });

        _supabaseMock
            .Setup(s => s.ActualizarAsync("gastos", It.IsAny<Guid>(), It.IsAny<Gasto>()))
            .ReturnsAsync((string _, object _, Gasto g) => g);

        var service = CrearService();
        var local = new Gasto
        {
            Id = gastoId, Monto = 100, CategoriaId = catId, UsuarioId = _usuarioId,
            CreadoEn = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            ActualizadoEn = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc),
        };

        await _storage.SetAsync("cdg_gastos", new List<Gasto> { local });

        local.Monto = 999;
        var actualizado = await service.ActualizarGastoAsync(local);

        Assert.True(actualizado.Sincronizado);
        Assert.Equal(999, actualizado.Monto);
        _supabaseMock.Verify(s => s.ActualizarAsync("gastos", gastoId, It.IsAny<Gasto>()), Times.Once);
    }
}
