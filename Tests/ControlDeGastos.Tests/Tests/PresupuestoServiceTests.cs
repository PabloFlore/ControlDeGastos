using Microsoft.Extensions.Logging;

namespace ControlDeGastos.Tests.Tests;

public class PresupuestoServiceTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<ISyncService> _syncMock = new();

    public PresupuestoServiceTests()
    {
        _serviceProviderMock
            .Setup(s => s.GetService(typeof(ISyncService)))
            .Returns(_syncMock.Object);
    }

    [Fact]
    public async Task ObtenerPresupuestosAsync_SinDatos_RetornaListaVacia()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var supabaseMock = new Mock<ISupabaseService>();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid() });

        var service = new PresupuestoService(storage, gastoServiceMock.Object, usuarioServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var result = await service.ObtenerPresupuestosAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task CrearY_ObtenerPresupuesto_FlujoCompleto()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var usuarioId = Guid.NewGuid();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = usuarioId });

        var service = new PresupuestoService(storage, gastoServiceMock.Object, usuarioServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var presupuesto = new Presupuesto
        {
            CategoriaId = Guid.NewGuid(),
            MontoLimite = 5000,
            Periodo = PeriodoPresupuesto.Mensual,
            FechaInicio = DateTime.UtcNow,
        };

        var creado = await service.CrearPresupuestoAsync(presupuesto);
        Assert.NotNull(creado);
        Assert.Equal(usuarioId, creado.UsuarioId);

        var lista = await service.ObtenerPresupuestosAsync();
        Assert.Single(lista);
        Assert.Equal(5000, lista[0].MontoLimite);
        Assert.Equal(PeriodoPresupuesto.Mensual, lista[0].Periodo);
    }

    [Fact]
    public async Task ObtenerGastadoEnPeriodoAsync_PeriodoMensual_FiltraCorrectamente()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var usuarioId = Guid.NewGuid();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = usuarioId });

        var categoriaId = Guid.NewGuid();
        var fechaInicio = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>
            {
                new() { Monto = 100, CategoriaId = categoriaId, Fecha = fechaInicio.AddDays(5) },
                new() { Monto = 200, CategoriaId = categoriaId, Fecha = fechaInicio.AddDays(15) },
                new() { Monto = 300, CategoriaId = Guid.NewGuid(), Fecha = fechaInicio.AddDays(10) }, // otra categoría
                new() { Monto = 400, CategoriaId = categoriaId, Fecha = fechaInicio.AddMonths(2) }, // fuera del período
            });

        var service = new PresupuestoService(storage, gastoServiceMock.Object, usuarioServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var presupuesto = new Presupuesto
        {
            CategoriaId = categoriaId,
            Periodo = PeriodoPresupuesto.Mensual,
            FechaInicio = fechaInicio,
        };

        var gastado = await service.ObtenerGastadoEnPeriodoAsync(presupuesto);
        Assert.Equal(300m, gastado); // 100 + 200 = 300, los otros 2 excluidos
    }

    [Fact]
    public async Task ObtenerGastadoEnPeriodoAsync_PeriodoAnual_SinCategoria_SumaTodos()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var usuarioId = Guid.NewGuid();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = usuarioId });

        var fechaInicio = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>
            {
                new() { Monto = 500, Fecha = fechaInicio.AddMonths(3) },
                new() { Monto = 300, Fecha = fechaInicio.AddMonths(6) },
                new() { Monto = 200, Fecha = fechaInicio.AddYears(2) }, // fuera del período
            });

        var service = new PresupuestoService(storage, gastoServiceMock.Object, usuarioServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var presupuesto = new Presupuesto
        {
            CategoriaId = null,
            Periodo = PeriodoPresupuesto.Anual,
            FechaInicio = fechaInicio,
        };

        var gastado = await service.ObtenerGastadoEnPeriodoAsync(presupuesto);
        Assert.Equal(800m, gastado);
    }

    [Fact]
    public async Task EliminarPresupuestoAsync_RemueveDeStorage()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid() });

        var service = new PresupuestoService(storage, new Mock<IGastoService>().Object, usuarioServiceMock.Object, new Mock<ISupabaseService>().Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var creado = await service.CrearPresupuestoAsync(new Presupuesto { MontoLimite = 1000, Periodo = PeriodoPresupuesto.Mensual });
        Assert.Single(await service.ObtenerPresupuestosAsync());

        await service.EliminarPresupuestoAsync(creado.Id);
        Assert.Empty(await service.ObtenerPresupuestosAsync());
    }

    [Fact]
    public async Task EliminarPresupuestoAsync_PlanNube_EliminaEnSupabase()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var supabaseMock = new Mock<ISupabaseService>();
        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Nube });

        var service = new PresupuestoService(storage, new Mock<IGastoService>().Object, usuarioServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var creado = await service.CrearPresupuestoAsync(new Presupuesto { MontoLimite = 2000, Periodo = PeriodoPresupuesto.Mensual });
        await service.EliminarPresupuestoAsync(creado.Id);

        supabaseMock.Verify(s => s.EliminarAsync<Presupuesto>("presupuestos", creado.Id), Times.Once);
    }

    [Fact]
    public async Task ActualizarPresupuestoAsync_ActualizaEnStorage()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid() });

        var service = new PresupuestoService(storage, new Mock<IGastoService>().Object, usuarioServiceMock.Object, new Mock<ISupabaseService>().Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var creado = await service.CrearPresupuestoAsync(new Presupuesto { MontoLimite = 1000, Periodo = PeriodoPresupuesto.Mensual });
        creado.MontoLimite = 5000;

        await service.ActualizarPresupuestoAsync(creado);

        var lista = await service.ObtenerPresupuestosAsync();
        Assert.Single(lista);
        Assert.Equal(5000, lista[0].MontoLimite);
    }

    [Fact]
    public async Task ActualizarPresupuestoAsync_IdNoExistente_NoFalla()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid() });

        var service = new PresupuestoService(storage, new Mock<IGastoService>().Object, usuarioServiceMock.Object, new Mock<ISupabaseService>().Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        await service.ActualizarPresupuestoAsync(new Presupuesto { Id = Guid.NewGuid(), MontoLimite = 9999 });

        Assert.Empty(await service.ObtenerPresupuestosAsync());
    }

    [Fact]
    public async Task ActualizarPresupuestoAsync_PlanNube_Sincroniza()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var supabaseMock = new Mock<ISupabaseService>();
        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Nube });

        supabaseMock
            .Setup(s => s.ActualizarAsync("presupuestos", It.IsAny<Guid>(), It.IsAny<Presupuesto>()))
            .ReturnsAsync((string _, Guid _, Presupuesto p) => p);

        var service = new PresupuestoService(storage, new Mock<IGastoService>().Object, usuarioServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var creado = await service.CrearPresupuestoAsync(new Presupuesto { MontoLimite = 1000, Periodo = PeriodoPresupuesto.Mensual });
        creado.MontoLimite = 9999;

        supabaseMock.Invocations.Clear();
        await service.ActualizarPresupuestoAsync(creado);

        supabaseMock.Verify(s => s.ActualizarAsync("presupuestos", creado.Id, It.IsAny<Presupuesto>()), Times.Once);
    }

    [Fact]
    public async Task ObtenerGastadoEnPeriodoAsync_PeriodoSemanal_FiltraCorrectamente()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var usuarioId = Guid.NewGuid();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = usuarioId });

        var categoriaId = Guid.NewGuid();
        var fechaInicio = new DateTime(2024, 6, 10, 0, 0, 0, DateTimeKind.Utc);

        gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>
            {
                new() { Monto = 100, CategoriaId = categoriaId, Fecha = fechaInicio.AddDays(1) },
                new() { Monto = 200, CategoriaId = categoriaId, Fecha = fechaInicio.AddDays(3) },
                new() { Monto = 300, CategoriaId = Guid.NewGuid(), Fecha = fechaInicio.AddDays(2) },
                new() { Monto = 400, CategoriaId = categoriaId, Fecha = fechaInicio.AddDays(10) },
            });

        var service = new PresupuestoService(storage, gastoServiceMock.Object, usuarioServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var presupuesto = new Presupuesto
        {
            CategoriaId = categoriaId,
            Periodo = PeriodoPresupuesto.Semanal,
            FechaInicio = fechaInicio,
        };

        var gastado = await service.ObtenerGastadoEnPeriodoAsync(presupuesto);
        Assert.Equal(300m, gastado);
    }

    [Fact]
    public async Task CalcularGastadoAsync_PeriodoSemanal_SinCategoria_SumaTodosEnRango()
    {
        var fechaInicio = new DateTime(2024, 6, 10, 0, 0, 0, DateTimeKind.Local);

        var presupuesto = new Presupuesto
        {
            Periodo = PeriodoPresupuesto.Semanal,
            FechaInicio = fechaInicio,
        };

        var gastos = new List<Gasto>
        {
            new() { Monto = 100, Fecha = fechaInicio.AddDays(1) },
            new() { Monto = 200, Fecha = fechaInicio.AddDays(6) },
            new() { Monto = 300, Fecha = fechaInicio.AddDays(8) },
        };

        var service = new PresupuestoService(
            new InMemoryStorageService(),
            new Mock<IGastoService>().Object,
            new Mock<IUsuarioService>().Object,
            new Mock<ISupabaseService>().Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var result = await service.CalcularGastadoAsync(presupuesto, gastos);
        Assert.Equal(300m, result);
    }

    [Fact]
    public async Task CalcularGastadoAsync_PeriodoMensual_ConCategoria_FiltraCorrectamente()
    {
        var categoriaId = Guid.NewGuid();
        var fechaInicio = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Local);

        var presupuesto = new Presupuesto
        {
            CategoriaId = categoriaId,
            Periodo = PeriodoPresupuesto.Mensual,
            FechaInicio = fechaInicio,
        };

        var gastos = new List<Gasto>
        {
            new() { Monto = 100, CategoriaId = categoriaId, Fecha = fechaInicio.AddDays(5) },
            new() { Monto = 200, CategoriaId = categoriaId, Fecha = fechaInicio.AddDays(15) },
            new() { Monto = 300, CategoriaId = Guid.NewGuid(), Fecha = fechaInicio.AddDays(10) },
            new() { Monto = 400, CategoriaId = categoriaId, Fecha = fechaInicio.AddMonths(2) },
        };

        var service = new PresupuestoService(
            new InMemoryStorageService(),
            new Mock<IGastoService>().Object,
            new Mock<IUsuarioService>().Object,
            new Mock<ISupabaseService>().Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var result = await service.CalcularGastadoAsync(presupuesto, gastos);
        Assert.Equal(300m, result);
    }

    [Fact]
    public async Task CalcularGastadoAsync_PeriodoAnual_FechaFinPersonalizada_UsaFechaFin()
    {
        var fechaInicio = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Local);
        var fechaFin = new DateTime(2024, 6, 30, 0, 0, 0, DateTimeKind.Local);

        var presupuesto = new Presupuesto
        {
            Periodo = PeriodoPresupuesto.Anual,
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
        };

        var gastos = new List<Gasto>
        {
            new() { Monto = 500, Fecha = fechaInicio.AddMonths(3) },
            new() { Monto = 300, Fecha = fechaInicio.AddMonths(5) },
            new() { Monto = 200, Fecha = fechaInicio.AddMonths(7) },
        };

        var service = new PresupuestoService(
            new InMemoryStorageService(),
            new Mock<IGastoService>().Object,
            new Mock<IUsuarioService>().Object,
            new Mock<ISupabaseService>().Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var result = await service.CalcularGastadoAsync(presupuesto, gastos);
        Assert.Equal(800m, result);
    }

    [Fact]
    public async Task CalcularGastadoAsync_SinGastos_RetornaCero()
    {
        var presupuesto = new Presupuesto
        {
            Periodo = PeriodoPresupuesto.Mensual,
            FechaInicio = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Local),
        };

        var service = new PresupuestoService(
            new InMemoryStorageService(),
            new Mock<IGastoService>().Object,
            new Mock<IUsuarioService>().Object,
            new Mock<ISupabaseService>().Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var result = await service.CalcularGastadoAsync(presupuesto, new List<Gasto>());
        Assert.Equal(0m, result);
    }

    [Fact]
    public async Task ObtenerPresupuestosAsync_ConHogar_FiltraPorHogar()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var hogarId = "hogar-test";

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), HogarId = hogarId });

        await storage.SetAsync("cdg_presupuestos", new List<Presupuesto>
        {
            new() { Id = Guid.NewGuid(), MontoLimite = 1000, Periodo = PeriodoPresupuesto.Mensual, HogarId = hogarId },
            new() { Id = Guid.NewGuid(), MontoLimite = 2000, Periodo = PeriodoPresupuesto.Mensual, HogarId = "otro-hogar" },
        });

        var service = new PresupuestoService(storage, gastoServiceMock.Object, usuarioServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var result = await service.ObtenerPresupuestosAsync();
        Assert.Single(result);
        Assert.Equal(1000, result[0].MontoLimite);
    }

    [Fact]
    public async Task CrearPresupuestoAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var supabaseMock = new Mock<ISupabaseService>();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Nube });

        supabaseMock
            .Setup(s => s.GuardarAsync("presupuestos", It.IsAny<Presupuesto>()))
            .ThrowsAsync(new Exception("Error de red"));

        var service = new PresupuestoService(storage, gastoServiceMock.Object, usuarioServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);
        var presupuesto = new Presupuesto { MontoLimite = 5000, Periodo = PeriodoPresupuesto.Mensual };

        var ex = await Record.ExceptionAsync(() => service.CrearPresupuestoAsync(presupuesto));
        Assert.Null(ex);
        Assert.Single(await service.ObtenerPresupuestosAsync());
    }

    [Fact]
    public async Task ActualizarPresupuestoAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var supabaseMock = new Mock<ISupabaseService>();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Nube });

        supabaseMock
            .Setup(s => s.ActualizarAsync("presupuestos", It.IsAny<Guid>(), It.IsAny<Presupuesto>()))
            .ThrowsAsync(new Exception("Error de red"));

        supabaseMock
            .Setup(s => s.GuardarAsync("presupuestos", It.IsAny<Presupuesto>()))
            .ReturnsAsync((string _, Presupuesto p) => p);

        var service = new PresupuestoService(storage, new Mock<IGastoService>().Object, usuarioServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);
        var creado = await service.CrearPresupuestoAsync(new Presupuesto { MontoLimite = 1000, Periodo = PeriodoPresupuesto.Mensual });

        creado.MontoLimite = 9999;
        var ex = await Record.ExceptionAsync(() => service.ActualizarPresupuestoAsync(creado));
        Assert.Null(ex);
        Assert.Equal(9999, (await service.ObtenerPresupuestosAsync())[0].MontoLimite);
    }

    [Fact]
    public async Task EliminarPresupuestoAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var supabaseMock = new Mock<ISupabaseService>();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Nube });

        supabaseMock
            .Setup(s => s.EliminarAsync<Presupuesto>("presupuestos", It.IsAny<Guid>()))
            .ThrowsAsync(new Exception("Error de red"));

        supabaseMock
            .Setup(s => s.GuardarAsync("presupuestos", It.IsAny<Presupuesto>()))
            .ReturnsAsync((string _, Presupuesto p) => p);

        var service = new PresupuestoService(storage, new Mock<IGastoService>().Object, usuarioServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);
        var creado = await service.CrearPresupuestoAsync(new Presupuesto { MontoLimite = 2000, Periodo = PeriodoPresupuesto.Mensual });

        var ex = await Record.ExceptionAsync(() => service.EliminarPresupuestoAsync(creado.Id));
        Assert.Null(ex);
        Assert.Empty(await service.ObtenerPresupuestosAsync());
    }

    [Fact]
    public async Task CalcularGastadoAsync_FechaUtc_ConvierteALocal()
    {
        var fechaInicio = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var presupuesto = new Presupuesto
        {
            Periodo = PeriodoPresupuesto.Mensual,
            FechaInicio = fechaInicio,
        };

        var gastos = new List<Gasto>
        {
            new() { Monto = 100, Fecha = fechaInicio.AddDays(1) },
            new() { Monto = 200, Fecha = fechaInicio.AddDays(30) },
            new() { Monto = 300, Fecha = fechaInicio.AddDays(32) },
        };

        var service = new PresupuestoService(
            new InMemoryStorageService(),
            new Mock<IGastoService>().Object,
            new Mock<IUsuarioService>().Object,
            new Mock<ISupabaseService>().Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var result = await service.CalcularGastadoAsync(presupuesto, gastos);
        Assert.Equal(300m, result);
    }

    [Fact]
    public async Task CalcularGastadoAsync_PeriodoInvalido_UsaDateTimeMaxValue()
    {
        var fechaInicio = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Local);

        var presupuesto = new Presupuesto
        {
            Periodo = (PeriodoPresupuesto)999,
            FechaInicio = fechaInicio,
        };

        var gastos = new List<Gasto>
        {
            new() { Monto = 100, Fecha = fechaInicio.AddDays(1) },
            new() { Monto = 200, Fecha = fechaInicio.AddDays(365 * 10) },
        };

        var service = new PresupuestoService(
            new InMemoryStorageService(),
            new Mock<IGastoService>().Object,
            new Mock<IUsuarioService>().Object,
            new Mock<ISupabaseService>().Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var result = await service.CalcularGastadoAsync(presupuesto, gastos);
        Assert.Equal(300m, result);
    }

    [Fact]
    public async Task FiltrarGastosParaPresupuestoAsync_AmbosFalsos_NoFiltra()
    {
        var usuarioServiceMock = new Mock<IUsuarioService>();
        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { ExcluirRecurrentesDePresupuesto = false, ExcluirCreditosDePresupuesto = false });

        var service = new PresupuestoService(
            new InMemoryStorageService(),
            new Mock<IGastoService>().Object,
            usuarioServiceMock.Object,
            new Mock<ISupabaseService>().Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var gastos = new List<Gasto>
        {
            new() { Monto = 100, RecurrenciaId = Guid.NewGuid() },
            new() { Monto = 200, FinanciamientoId = Guid.NewGuid() },
            new() { Monto = 300 },
        };

        var result = await service.FiltrarGastosParaPresupuestoAsync(gastos);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task FiltrarGastosParaPresupuestoAsync_ExcluirRecurrentes_RemueveRecurrencias()
    {
        var usuarioServiceMock = new Mock<IUsuarioService>();
        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { ExcluirRecurrentesDePresupuesto = true, ExcluirCreditosDePresupuesto = false });

        var service = new PresupuestoService(
            new InMemoryStorageService(),
            new Mock<IGastoService>().Object,
            usuarioServiceMock.Object,
            new Mock<ISupabaseService>().Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var gastos = new List<Gasto>
        {
            new() { Monto = 100, RecurrenciaId = Guid.NewGuid() },
            new() { Monto = 200, FinanciamientoId = Guid.NewGuid() },
            new() { Monto = 300 },
        };

        var result = await service.FiltrarGastosParaPresupuestoAsync(gastos);
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, g => g.RecurrenciaId != null);
    }

    [Fact]
    public async Task FiltrarGastosParaPresupuestoAsync_ExcluirCreditos_RemueveCreditos()
    {
        var usuarioServiceMock = new Mock<IUsuarioService>();
        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { ExcluirRecurrentesDePresupuesto = false, ExcluirCreditosDePresupuesto = true });

        var service = new PresupuestoService(
            new InMemoryStorageService(),
            new Mock<IGastoService>().Object,
            usuarioServiceMock.Object,
            new Mock<ISupabaseService>().Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var gastos = new List<Gasto>
        {
            new() { Monto = 100, RecurrenciaId = Guid.NewGuid() },
            new() { Monto = 200, FinanciamientoId = Guid.NewGuid() },
            new() { Monto = 300 },
        };

        var result = await service.FiltrarGastosParaPresupuestoAsync(gastos);
        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, g => g.FinanciamientoId != null);
    }

    [Fact]
    public async Task FiltrarGastosParaPresupuestoAsync_AmbosVerdaderos_RemueveAmbos()
    {
        var usuarioServiceMock = new Mock<IUsuarioService>();
        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { ExcluirRecurrentesDePresupuesto = true, ExcluirCreditosDePresupuesto = true });

        var service = new PresupuestoService(
            new InMemoryStorageService(),
            new Mock<IGastoService>().Object,
            usuarioServiceMock.Object,
            new Mock<ISupabaseService>().Object, _serviceProviderMock.Object, new Mock<ILogger<PresupuestoService>>().Object);

        var gastos = new List<Gasto>
        {
            new() { Monto = 100, RecurrenciaId = Guid.NewGuid() },
            new() { Monto = 200, FinanciamientoId = Guid.NewGuid() },
            new() { Monto = 300, RecurrenciaId = Guid.NewGuid(), FinanciamientoId = Guid.NewGuid() },
            new() { Monto = 400 },
        };

        var result = await service.FiltrarGastosParaPresupuestoAsync(gastos);
        Assert.Single(result);
        Assert.Equal(400, result[0].Monto);
    }
}
