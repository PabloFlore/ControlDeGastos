using Microsoft.Extensions.Logging;

namespace ControlDeGastos.Tests.Tests;

public class RecurrenciaServiceTests
{
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<ISyncService> _syncMock = new();

    public RecurrenciaServiceTests()
    {
        _serviceProviderMock
            .Setup(s => s.GetService(typeof(ISyncService)))
            .Returns(_syncMock.Object);
    }

    [Fact]
    public async Task ObtenerRecurrenciasAsync_SinDatos_RetornaListaVacia()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid() });

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        var result = await service.ObtenerRecurrenciasAsync();
        Assert.Empty(result);
    }

    [Fact]
    public async Task CrearRecurrenciaAsync_Mensual_AsignaProximaFechaCorrecta()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var usuarioId = Guid.NewGuid();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = usuarioId, PlanActivo = PlanType.Local });

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        var fechaInicio = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var recurrencia = new Recurrencia
        {
            CategoriaId = Guid.NewGuid(),
            Monto = 1000,
            TipoRecurrencia = TipoRecurrencia.Mensual,
            FechaInicio = fechaInicio,
            Descripcion = "Test",
        };

        var creada = await service.CrearRecurrenciaAsync(recurrencia);
        Assert.NotNull(creada);
        Assert.Equal(usuarioId, creada.UsuarioId);
        Assert.Equal(fechaInicio.AddMonths(1), creada.ProximaFecha);
    }

    [Fact]
    public async Task CrearRecurrenciaAsync_Semanal_AsignaProximaFechaCorrecta()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var usuarioId = Guid.NewGuid();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = usuarioId, PlanActivo = PlanType.Local });

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        var fechaInicio = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var recurrencia = new Recurrencia
        {
            CategoriaId = Guid.NewGuid(),
            Monto = 500,
            TipoRecurrencia = TipoRecurrencia.Semanal,
            FechaInicio = fechaInicio,
            Intervalo = 2,
        };

        var creada = await service.CrearRecurrenciaAsync(recurrencia);
        Assert.Equal(fechaInicio.AddDays(14), creada.ProximaFecha); // 7 * 2
    }

    [Fact]
    public async Task CrearRecurrenciaAsync_FechaInicioEnPasado_CreaGastoInmediato()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var usuarioId = Guid.NewGuid();
        var categoriaId = Guid.NewGuid();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = usuarioId, PlanActivo = PlanType.Local });

        Gasto? gastoCreado = null;
        gastoServiceMock
            .Setup(s => s.CrearGastoAsync(It.IsAny<Gasto>()))
            .Callback<Gasto>(g => gastoCreado = g)
            .ReturnsAsync((Gasto g) => g);

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        var fechaInicio = DateTime.UtcNow.AddDays(-5);

        var recurrencia = new Recurrencia
        {
            CategoriaId = categoriaId,
            Monto = 1500,
            TipoRecurrencia = TipoRecurrencia.Mensual,
            FechaInicio = fechaInicio,
        };

        await service.CrearRecurrenciaAsync(recurrencia);
        Assert.NotNull(gastoCreado);
        Assert.Equal(categoriaId, gastoCreado.CategoriaId);
        Assert.Equal(1500, gastoCreado.Monto);
    }

    [Fact]
    public async Task GenerarPendientesAsync_RecurrenciaVencida_CreaGastoYActualizaFecha()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var usuarioId = Guid.NewGuid();
        var categoriaId = Guid.NewGuid();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = usuarioId, PlanActivo = PlanType.Local });

        gastoServiceMock
            .Setup(s => s.CrearGastoAsync(It.IsAny<Gasto>()))
            .ReturnsAsync((Gasto g) => g);

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        var recurrencia = new Recurrencia
        {
            UsuarioId = usuarioId,
            CategoriaId = categoriaId,
            Monto = 2000,
            TipoRecurrencia = TipoRecurrencia.Mensual,
            FechaInicio = DateTime.UtcNow.AddMonths(-3),
            ProximaFecha = DateTime.UtcNow.AddDays(-1), // vencida
            Activa = true,
        };

        // Guardamos directo en storage para evitar que CrearRecurrenciaAsync recalcule ProximaFecha
        await storage.SetAsync("cdg_recurrencias", new List<Recurrencia> { recurrencia });

        var generados = await service.GenerarPendientesAsync();

        Assert.NotEmpty(generados);
        Assert.Equal(2000, generados[0].Monto);

        var recurrencias = await service.ObtenerRecurrenciasAsync();
        var updated = recurrencias.First();
        Assert.True(updated.ProximaFecha > DateTime.UtcNow.AddDays(-1));
    }

    [Fact]
    public async Task GenerarPendientesAsync_SinPendientes_NoGeneraGastos()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var usuarioId = Guid.NewGuid();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = usuarioId, PlanActivo = PlanType.Local });

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        var recurrencia = new Recurrencia
        {
            UsuarioId = usuarioId,
            CategoriaId = Guid.NewGuid(),
            Monto = 100,
            TipoRecurrencia = TipoRecurrencia.Mensual,
            FechaInicio = DateTime.UtcNow,
            ProximaFecha = DateTime.UtcNow.AddDays(10), // aún no vence
            Activa = true,
        };

        await service.CrearRecurrenciaAsync(recurrencia);
        var generados = await service.GenerarPendientesAsync();
        Assert.Empty(generados);
    }

    [Fact]
    public async Task EliminarRecurrenciaAsync_RemueveDeLaLista()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Local });

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        var rec = new Recurrencia
        {
            CategoriaId = Guid.NewGuid(),
            Monto = 500,
            TipoRecurrencia = TipoRecurrencia.Mensual,
        };

        await service.CrearRecurrenciaAsync(rec);
        Assert.Single(await service.ObtenerRecurrenciasAsync());

        await service.EliminarRecurrenciaAsync(rec.Id);
        Assert.Empty(await service.ObtenerRecurrenciasAsync());
    }

    [Fact]
    public async Task ActualizarRecurrenciaAsync_ActualizaEnStorage()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Local });

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        var rec = new Recurrencia
        {
            CategoriaId = Guid.NewGuid(),
            Monto = 500,
            TipoRecurrencia = TipoRecurrencia.Mensual,
            Descripcion = "Original",
        };

        var creada = await service.CrearRecurrenciaAsync(rec);
        creada.Descripcion = "Actualizada";
        creada.Monto = 999;

        await service.ActualizarRecurrenciaAsync(creada);

        var lista = await service.ObtenerRecurrenciasAsync();
        Assert.Single(lista);
        Assert.Equal("Actualizada", lista[0].Descripcion);
        Assert.Equal(999, lista[0].Monto);
    }

    [Fact]
    public async Task ActualizarRecurrenciaAsync_IdNoExistente_NoFalla()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Local });

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, new Mock<IGastoService>().Object, new Mock<ISupabaseService>().Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        await service.ActualizarRecurrenciaAsync(new Recurrencia { Id = Guid.NewGuid() });
        Assert.Empty(await service.ObtenerRecurrenciasAsync());
    }

    [Fact]
    public async Task ActualizarRecurrenciaAsync_PlanNube_Sincroniza()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var supabaseMock = new Mock<ISupabaseService>();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Nube });

        supabaseMock
            .Setup(s => s.ActualizarAsync("recurrencias", It.IsAny<Guid>(), It.IsAny<Recurrencia>()))
            .ReturnsAsync((string _, Guid _, Recurrencia r) => r);

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, new Mock<IGastoService>().Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        var rec = new Recurrencia
        {
            CategoriaId = Guid.NewGuid(),
            Monto = 500,
            TipoRecurrencia = TipoRecurrencia.Mensual,
        };

        var creada = await service.CrearRecurrenciaAsync(rec);
        supabaseMock.Invocations.Clear();

        creada.Descripcion = "Sincronizada";
        await service.ActualizarRecurrenciaAsync(creada);

        supabaseMock.Verify(s => s.ActualizarAsync("recurrencias", creada.Id, It.IsAny<Recurrencia>()), Times.Once);
    }

    [Fact]
    public async Task EliminarRecurrenciaConGastosAsync_EliminaRecurrenciaYSusGastos()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Local });

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        var rec = new Recurrencia
        {
            CategoriaId = Guid.NewGuid(),
            Monto = 1000,
            TipoRecurrencia = TipoRecurrencia.Mensual,
        };

        var creada = await service.CrearRecurrenciaAsync(rec);

        gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>
            {
                new() { Id = Guid.NewGuid(), RecurrenciaId = creada.Id, Monto = 1000, CategoriaId = Guid.NewGuid() },
                new() { Id = Guid.NewGuid(), RecurrenciaId = creada.Id, Monto = 1000, CategoriaId = Guid.NewGuid() },
                new() { Id = Guid.NewGuid(), Monto = 500, CategoriaId = Guid.NewGuid() },
            });

        await service.EliminarRecurrenciaConGastosAsync(creada.Id);

        gastoServiceMock.Verify(s => s.EliminarGastoAsync(It.IsAny<Guid>()), Times.Exactly(2));
        Assert.Empty(await service.ObtenerRecurrenciasAsync());
    }

    [Fact]
    public async Task ObtenerRecurrenciasAsync_ConHogar_FiltraPorHogar()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var hogarId = "hogar-test";

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), HogarId = hogarId });

        await storage.SetAsync("cdg_recurrencias", new List<Recurrencia>
        {
            new() { Id = Guid.NewGuid(), Monto = 500, TipoRecurrencia = TipoRecurrencia.Mensual, HogarId = hogarId },
            new() { Id = Guid.NewGuid(), Monto = 300, TipoRecurrencia = TipoRecurrencia.Mensual, HogarId = "otro-hogar" },
        });

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);
        var result = await service.ObtenerRecurrenciasAsync();

        Assert.Single(result);
        Assert.Equal(500, result[0].Monto);
    }

    [Fact]
    public async Task CrearRecurrenciaAsync_Diario_AsignaProximaFechaCorrecta()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Local });

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);
        var fechaInicio = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var recurrencia = new Recurrencia
        {
            CategoriaId = Guid.NewGuid(), Monto = 500, TipoRecurrencia = TipoRecurrencia.Diario,
            FechaInicio = fechaInicio, Intervalo = 3,
        };

        var creada = await service.CrearRecurrenciaAsync(recurrencia);
        Assert.Equal(fechaInicio.AddDays(3), creada.ProximaFecha);
    }

    [Fact]
    public async Task CrearRecurrenciaAsync_Anual_AsignaProximaFechaCorrecta()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Local });

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);
        var fechaInicio = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var recurrencia = new Recurrencia
        {
            CategoriaId = Guid.NewGuid(), Monto = 1000, TipoRecurrencia = TipoRecurrencia.Anual,
            FechaInicio = fechaInicio,
        };

        var creada = await service.CrearRecurrenciaAsync(recurrencia);
        Assert.Equal(fechaInicio.AddYears(1), creada.ProximaFecha);
    }

    [Fact]
    public async Task CrearRecurrenciaAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Nube });

        supabaseMock
            .Setup(s => s.GuardarAsync("recurrencias", It.IsAny<Recurrencia>()))
            .ThrowsAsync(new Exception("Error de red"));

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);
        var recurrencia = new Recurrencia
        {
            CategoriaId = Guid.NewGuid(), Monto = 500, TipoRecurrencia = TipoRecurrencia.Mensual,
        };

        var ex = await Record.ExceptionAsync(() => service.CrearRecurrenciaAsync(recurrencia));
        Assert.Null(ex);
        Assert.Single(await service.ObtenerRecurrenciasAsync());
    }

    [Fact]
    public async Task ActualizarRecurrenciaAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Nube });

        supabaseMock
            .Setup(s => s.ActualizarAsync("recurrencias", It.IsAny<Guid>(), It.IsAny<Recurrencia>()))
            .ThrowsAsync(new Exception("Error de red"));

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);
        var rec = new Recurrencia
        {
            CategoriaId = Guid.NewGuid(), Monto = 500, TipoRecurrencia = TipoRecurrencia.Mensual,
        };
        var creada = await service.CrearRecurrenciaAsync(rec);

        creada.Monto = 999;
        var ex = await Record.ExceptionAsync(() => service.ActualizarRecurrenciaAsync(creada));
        Assert.Null(ex);
        Assert.Equal(999, (await service.ObtenerRecurrenciasAsync())[0].Monto);
    }

    [Fact]
    public async Task EliminarRecurrenciaAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), PlanActivo = PlanType.Nube });

        supabaseMock
            .Setup(s => s.EliminarAsync<Recurrencia>("recurrencias", It.IsAny<Guid>()))
            .ThrowsAsync(new Exception("Error de red"));

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);
        var rec = new Recurrencia
        {
            CategoriaId = Guid.NewGuid(), Monto = 500, TipoRecurrencia = TipoRecurrencia.Mensual,
        };
        var creada = await service.CrearRecurrenciaAsync(rec);

        var ex = await Record.ExceptionAsync(() => service.EliminarRecurrenciaAsync(creada.Id));
        Assert.Null(ex);
        Assert.Empty(await service.ObtenerRecurrenciasAsync());
    }

    [Fact]
    public async Task GenerarPendientesAsync_TipoDiario_ActualizaProximaFecha()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var usuarioId = Guid.NewGuid();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = usuarioId, PlanActivo = PlanType.Local });

        gastoServiceMock
            .Setup(s => s.CrearGastoAsync(It.IsAny<Gasto>()))
            .ReturnsAsync((Gasto g) => g);

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        var recurrencia = new Recurrencia
        {
            UsuarioId = usuarioId, CategoriaId = Guid.NewGuid(), Monto = 100,
            TipoRecurrencia = TipoRecurrencia.Diario, FechaInicio = DateTime.UtcNow.AddDays(-3),
            ProximaFecha = DateTime.UtcNow.AddDays(-1), Activa = true, Intervalo = 1,
        };

        await storage.SetAsync("cdg_recurrencias", new List<Recurrencia> { recurrencia });

        var generados = await service.GenerarPendientesAsync();
        Assert.NotEmpty(generados);

        var recurrencias = await service.ObtenerRecurrenciasAsync();
        var updated = recurrencias.First();
        Assert.True(updated.ProximaFecha > DateTime.UtcNow.AddDays(-1));
    }

    [Fact]
    public async Task GenerarPendientesAsync_TipoSemanal_ActualizaProximaFecha()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var usuarioId = Guid.NewGuid();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = usuarioId, PlanActivo = PlanType.Local });

        gastoServiceMock
            .Setup(s => s.CrearGastoAsync(It.IsAny<Gasto>()))
            .ReturnsAsync((Gasto g) => g);

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        var recurrencia = new Recurrencia
        {
            UsuarioId = usuarioId, CategoriaId = Guid.NewGuid(), Monto = 200,
            TipoRecurrencia = TipoRecurrencia.Semanal, FechaInicio = DateTime.UtcNow.AddDays(-10),
            ProximaFecha = DateTime.UtcNow.AddDays(-1), Activa = true, Intervalo = 1,
        };

        await storage.SetAsync("cdg_recurrencias", new List<Recurrencia> { recurrencia });

        var generados = await service.GenerarPendientesAsync();
        Assert.NotEmpty(generados);

        var recurrencias = await service.ObtenerRecurrenciasAsync();
        var updated = recurrencias.First();
        Assert.True(updated.ProximaFecha > DateTime.UtcNow.AddDays(-1));
    }

    [Fact]
    public async Task GenerarPendientesAsync_TipoAnual_ActualizaProximaFecha()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var usuarioId = Guid.NewGuid();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = usuarioId, PlanActivo = PlanType.Local });

        gastoServiceMock
            .Setup(s => s.CrearGastoAsync(It.IsAny<Gasto>()))
            .ReturnsAsync((Gasto g) => g);

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        var recurrencia = new Recurrencia
        {
            UsuarioId = usuarioId, CategoriaId = Guid.NewGuid(), Monto = 1000,
            TipoRecurrencia = TipoRecurrencia.Anual, FechaInicio = DateTime.UtcNow.AddYears(-2),
            ProximaFecha = DateTime.UtcNow.AddDays(-1), Activa = true,
        };

        await storage.SetAsync("cdg_recurrencias", new List<Recurrencia> { recurrencia });

        var generados = await service.GenerarPendientesAsync();
        Assert.NotEmpty(generados);

        var recurrencias = await service.ObtenerRecurrenciasAsync();
        var updated = recurrencias.First();
        Assert.True(updated.ProximaFecha > DateTime.UtcNow.AddDays(-1));
    }

    [Fact]
    public async Task GenerarPendientesAsync_TipoDesconocido_UsaDefaultMensual()
    {
        var storage = new InMemoryStorageService();
        var usuarioServiceMock = new Mock<IUsuarioService>();
        var gastoServiceMock = new Mock<IGastoService>();
        var supabaseMock = new Mock<ISupabaseService>();
        var usuarioId = Guid.NewGuid();

        usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = usuarioId, PlanActivo = PlanType.Local });

        gastoServiceMock
            .Setup(s => s.CrearGastoAsync(It.IsAny<Gasto>()))
            .ReturnsAsync((Gasto g) => g);

        var service = new RecurrenciaService(storage, usuarioServiceMock.Object, gastoServiceMock.Object, supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<RecurrenciaService>>().Object);

        var recurrencia = new Recurrencia
        {
            UsuarioId = usuarioId, CategoriaId = Guid.NewGuid(), Monto = 500,
            TipoRecurrencia = (TipoRecurrencia)999, FechaInicio = DateTime.UtcNow.AddMonths(-3),
            ProximaFecha = DateTime.UtcNow.AddDays(-1), Activa = true,
        };

        await storage.SetAsync("cdg_recurrencias", new List<Recurrencia> { recurrencia });

        var generados = await service.GenerarPendientesAsync();
        Assert.NotEmpty(generados);

        var recurrencias = await service.ObtenerRecurrenciasAsync();
        var updated = recurrencias.First();
        Assert.True(updated.ProximaFecha > DateTime.UtcNow.AddDays(-1));
    }
}
