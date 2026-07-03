namespace ControlDeGastos.Tests.Tests;

public class NotificacionServiceTests
{
    [Fact]
    public async Task VerificarNotificacionesAsync_SinRecurrenciasNiPresupuestos_RetornaVacio()
    {
        var storage = new InMemoryStorageService();
        var recurrenciaMock = new Mock<IRecurrenciaService>();
        var presupuestoMock = new Mock<IPresupuestoService>();
        var categoriaMock = new Mock<ICategoriaService>();

        recurrenciaMock.Setup(s => s.ObtenerRecurrenciasAsync()).ReturnsAsync(new List<Recurrencia>());
        presupuestoMock.Setup(s => s.ObtenerPresupuestosAsync()).ReturnsAsync(new List<Presupuesto>());
        categoriaMock.Setup(s => s.ObtenerCategoriasAsync()).ReturnsAsync(new List<Categoria>());

        var service = new NotificacionService(storage, recurrenciaMock.Object, presupuestoMock.Object, categoriaMock.Object);
        var result = await service.VerificarNotificacionesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task VerificarNotificacionesAsync_RecurrenciaVenceHoy_RetornaNotificacion()
    {
        var storage = new InMemoryStorageService();
        var recurrenciaMock = new Mock<IRecurrenciaService>();
        var presupuestoMock = new Mock<IPresupuestoService>();
        var categoriaMock = new Mock<ICategoriaService>();

        var recurrencia = new Recurrencia
        {
            Id = Guid.NewGuid(),
            Activa = true,
            ProximaFecha = DateTime.Now.Date.AddHours(12),
            Monto = 500,
            Descripcion = "Renta",
            TipoRecurrencia = TipoRecurrencia.Mensual,
        };

        recurrenciaMock.Setup(s => s.ObtenerRecurrenciasAsync()).ReturnsAsync(new List<Recurrencia> { recurrencia });
        presupuestoMock.Setup(s => s.ObtenerPresupuestosAsync()).ReturnsAsync(new List<Presupuesto>());
        categoriaMock.Setup(s => s.ObtenerCategoriasAsync()).ReturnsAsync(new List<Categoria>());

        var service = new NotificacionService(storage, recurrenciaMock.Object, presupuestoMock.Object, categoriaMock.Object);
        var result = await service.VerificarNotificacionesAsync();

        Assert.Contains(result, n => n.Tipo == "recurrencia" && n.Mensaje.Contains("hoy"));
    }

    [Fact]
    public async Task VerificarNotificacionesAsync_RecurrenciaInactiva_NoGeneraNotificacion()
    {
        var storage = new InMemoryStorageService();
        var recurrenciaMock = new Mock<IRecurrenciaService>();
        var presupuestoMock = new Mock<IPresupuestoService>();
        var categoriaMock = new Mock<ICategoriaService>();

        var recurrencia = new Recurrencia
        {
            Id = Guid.NewGuid(),
            Activa = false,
            ProximaFecha = DateTime.UtcNow.Date.AddHours(12),
            Monto = 500,
        };

        recurrenciaMock.Setup(s => s.ObtenerRecurrenciasAsync()).ReturnsAsync(new List<Recurrencia> { recurrencia });
        presupuestoMock.Setup(s => s.ObtenerPresupuestosAsync()).ReturnsAsync(new List<Presupuesto>());
        categoriaMock.Setup(s => s.ObtenerCategoriasAsync()).ReturnsAsync(new List<Categoria>());

        var service = new NotificacionService(storage, recurrenciaMock.Object, presupuestoMock.Object, categoriaMock.Object);
        var result = await service.VerificarNotificacionesAsync();

        Assert.DoesNotContain(result, n => n.Tipo == "recurrencia");
    }

    [Fact]
    public async Task VerificarNotificacionesAsync_RecurrenciaVencida_NoGeneraNotificacion()
    {
        var storage = new InMemoryStorageService();
        var recurrenciaMock = new Mock<IRecurrenciaService>();
        var presupuestoMock = new Mock<IPresupuestoService>();
        var categoriaMock = new Mock<ICategoriaService>();

        var recurrencia = new Recurrencia
        {
            Id = Guid.NewGuid(),
            Activa = true,
            ProximaFecha = DateTime.UtcNow.Date.AddDays(-5),
            Monto = 500,
        };

        recurrenciaMock.Setup(s => s.ObtenerRecurrenciasAsync()).ReturnsAsync(new List<Recurrencia> { recurrencia });
        presupuestoMock.Setup(s => s.ObtenerPresupuestosAsync()).ReturnsAsync(new List<Presupuesto>());
        categoriaMock.Setup(s => s.ObtenerCategoriasAsync()).ReturnsAsync(new List<Categoria>());

        var service = new NotificacionService(storage, recurrenciaMock.Object, presupuestoMock.Object, categoriaMock.Object);
        var result = await service.VerificarNotificacionesAsync();

        Assert.DoesNotContain(result, n => n.Tipo == "recurrencia");
    }

    [Fact]
    public async Task VerificarNotificacionesAsync_RecurrenciaConFechaFinExpirada_NoGenera()
    {
        var storage = new InMemoryStorageService();
        var recurrenciaMock = new Mock<IRecurrenciaService>();
        var presupuestoMock = new Mock<IPresupuestoService>();
        var categoriaMock = new Mock<ICategoriaService>();

        var recurrencia = new Recurrencia
        {
            Id = Guid.NewGuid(),
            Activa = true,
            ProximaFecha = DateTime.UtcNow.Date.AddHours(12),
            FechaFin = DateTime.UtcNow.Date.AddDays(-1),
            Monto = 500,
        };

        recurrenciaMock.Setup(s => s.ObtenerRecurrenciasAsync()).ReturnsAsync(new List<Recurrencia> { recurrencia });
        presupuestoMock.Setup(s => s.ObtenerPresupuestosAsync()).ReturnsAsync(new List<Presupuesto>());
        categoriaMock.Setup(s => s.ObtenerCategoriasAsync()).ReturnsAsync(new List<Categoria>());

        var service = new NotificacionService(storage, recurrenciaMock.Object, presupuestoMock.Object, categoriaMock.Object);
        var result = await service.VerificarNotificacionesAsync();

        Assert.DoesNotContain(result, n => n.Tipo == "recurrencia");
    }

    [Fact]
    public async Task VerificarNotificacionesAsync_PresupuestoExcedido_GeneraAlertaRoja()
    {
        var storage = new InMemoryStorageService();
        var recurrenciaMock = new Mock<IRecurrenciaService>();
        var presupuestoMock = new Mock<IPresupuestoService>();
        var categoriaMock = new Mock<ICategoriaService>();

        var catId = Guid.NewGuid();
        var presupuesto = new Presupuesto
        {
            Id = Guid.NewGuid(),
            CategoriaId = catId,
            MontoLimite = 1000,
            Periodo = PeriodoPresupuesto.Mensual,
            FechaInicio = DateTime.UtcNow.AddDays(-15),
        };

        recurrenciaMock.Setup(s => s.ObtenerRecurrenciasAsync()).ReturnsAsync(new List<Recurrencia>());
        presupuestoMock.Setup(s => s.ObtenerPresupuestosAsync()).ReturnsAsync(new List<Presupuesto> { presupuesto });
        presupuestoMock.Setup(s => s.ObtenerGastadoEnPeriodoAsync(presupuesto)).ReturnsAsync(1200m);
        categoriaMock.Setup(s => s.ObtenerCategoriasAsync()).ReturnsAsync(new List<Categoria>
        {
            new() { Id = catId, Nombre = "Comida", Icono = "🍔", Color = "#ff0000" }
        });

        var service = new NotificacionService(storage, recurrenciaMock.Object, presupuestoMock.Object, categoriaMock.Object);
        var result = await service.VerificarNotificacionesAsync();

        Assert.Contains(result, n => n.Tipo == "presupuesto_excedido" && n.Mensaje.Contains("excedido"));
    }

    [Fact]
    public async Task VerificarNotificacionesAsync_PresupuestoAl80_GeneraAlertaAmarilla()
    {
        var storage = new InMemoryStorageService();
        var recurrenciaMock = new Mock<IRecurrenciaService>();
        var presupuestoMock = new Mock<IPresupuestoService>();
        var categoriaMock = new Mock<ICategoriaService>();

        var catId = Guid.NewGuid();
        var presupuesto = new Presupuesto
        {
            Id = Guid.NewGuid(),
            CategoriaId = catId,
            MontoLimite = 1000,
            Periodo = PeriodoPresupuesto.Mensual,
            FechaInicio = DateTime.UtcNow.AddDays(-15),
        };

        recurrenciaMock.Setup(s => s.ObtenerRecurrenciasAsync()).ReturnsAsync(new List<Recurrencia>());
        presupuestoMock.Setup(s => s.ObtenerPresupuestosAsync()).ReturnsAsync(new List<Presupuesto> { presupuesto });
        presupuestoMock.Setup(s => s.ObtenerGastadoEnPeriodoAsync(presupuesto)).ReturnsAsync(850m);
        categoriaMock.Setup(s => s.ObtenerCategoriasAsync()).ReturnsAsync(new List<Categoria>
        {
            new() { Id = catId, Nombre = "Comida", Icono = "🍔", Color = "#ff0000" }
        });

        var service = new NotificacionService(storage, recurrenciaMock.Object, presupuestoMock.Object, categoriaMock.Object);
        var result = await service.VerificarNotificacionesAsync();

        Assert.Contains(result, n => n.Tipo == "presupuesto_alerta");
    }

    [Fact]
    public async Task VerificarNotificacionesAsync_MismaNotificacionNoSeRepiteEnSesion()
    {
        var storage = new InMemoryStorageService();
        var recurrenciaMock = new Mock<IRecurrenciaService>();
        var presupuestoMock = new Mock<IPresupuestoService>();
        var categoriaMock = new Mock<ICategoriaService>();

        var recurrencia = new Recurrencia
        {
            Id = Guid.NewGuid(),
            Activa = true,
            ProximaFecha = DateTime.UtcNow.Date.AddHours(12),
            Monto = 500,
        };

        recurrenciaMock.Setup(s => s.ObtenerRecurrenciasAsync()).ReturnsAsync(new List<Recurrencia> { recurrencia });
        presupuestoMock.Setup(s => s.ObtenerPresupuestosAsync()).ReturnsAsync(new List<Presupuesto>());
        categoriaMock.Setup(s => s.ObtenerCategoriasAsync()).ReturnsAsync(new List<Categoria>());

        var service = new NotificacionService(storage, recurrenciaMock.Object, presupuestoMock.Object, categoriaMock.Object);

        var primera = await service.VerificarNotificacionesAsync();
        var segunda = await service.VerificarNotificacionesAsync();

        Assert.NotEmpty(primera);
        Assert.Empty(segunda);
    }

    [Fact]
    public async Task VerificarNotificacionesAsync_LimpiaIdsVistosViejos()
    {
        var storage = new InMemoryStorageService();
        var recurrenciaMock = new Mock<IRecurrenciaService>();
        var presupuestoMock = new Mock<IPresupuestoService>();
        var categoriaMock = new Mock<ICategoriaService>();

        var fechaVieja = DateTime.Now.Date.AddDays(-10).ToString("yyyyMMdd");
        var mapViejo = new Dictionary<string, List<Guid>>
        {
            [fechaVieja] = new() { Guid.NewGuid() },
        };
        await storage.SetAsync("cdg_notif_vistas_map", mapViejo);

        recurrenciaMock.Setup(s => s.ObtenerRecurrenciasAsync()).ReturnsAsync(new List<Recurrencia>());
        presupuestoMock.Setup(s => s.ObtenerPresupuestosAsync()).ReturnsAsync(new List<Presupuesto>());
        categoriaMock.Setup(s => s.ObtenerCategoriasAsync()).ReturnsAsync(new List<Categoria>());

        var service = new NotificacionService(storage, recurrenciaMock.Object, presupuestoMock.Object, categoriaMock.Object);
        await service.VerificarNotificacionesAsync();

        var map = await storage.GetAsync<Dictionary<string, List<Guid>>>("cdg_notif_vistas_map");
        Assert.NotNull(map);
        Assert.False(map!.ContainsKey(fechaVieja));
    }
}
