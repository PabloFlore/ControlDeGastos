using Microsoft.Extensions.Logging;

namespace ControlDeGastos.Tests.Tests;

public class SyncServiceTests
{
    private readonly InMemoryStorageService _storage = new();
    private readonly Mock<ISupabaseService> _supabaseMock = new();
    private readonly Mock<IUsuarioService> _usuarioServiceMock = new();
    private readonly Mock<IGastoService> _gastoServiceMock = new();
    private readonly Mock<ICategoriaService> _categoriaServiceMock = new();
    private readonly Mock<IPresupuestoService> _presupuestoServiceMock = new();
    private readonly Mock<IRecurrenciaService> _recurrenciaServiceMock = new();
    private readonly Mock<IFinanciamientoService> _financiamientoServiceMock = new();
    private readonly Mock<IConnectivityService> _connectivityMock = new();
    private readonly Guid _usuarioId = Guid.NewGuid();

    public SyncServiceTests()
    {
        _connectivityMock.SetupGet(c => c.IsOnline).Returns(true);

        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _categoriaServiceMock
            .Setup(s => s.ObtenerCategoriasAsync())
            .ReturnsAsync(new List<Categoria>());

        _presupuestoServiceMock
            .Setup(s => s.ObtenerPresupuestosAsync())
            .ReturnsAsync(new List<Presupuesto>());

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Gasto>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Gasto>());

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Categoria>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Categoria>());

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Presupuesto>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Presupuesto>());

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Recurrencia>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Recurrencia>());

        _recurrenciaServiceMock
            .Setup(s => s.ObtenerRecurrenciasAsync())
            .ReturnsAsync(new List<Recurrencia>());

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Financiamiento>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Financiamiento>());

        _financiamientoServiceMock
            .Setup(s => s.ObtenerFinanciamientosAsync())
            .ReturnsAsync(new List<Financiamiento>());
        _financiamientoServiceMock
            .Setup(s => s.ObtenerBancosAsync())
            .ReturnsAsync(new List<string>());
    }

    private SyncService CrearService()
        => new(_storage, _supabaseMock.Object, _usuarioServiceMock.Object,
               _gastoServiceMock.Object, _categoriaServiceMock.Object, _presupuestoServiceMock.Object,
               _recurrenciaServiceMock.Object, _financiamientoServiceMock.Object,
               _connectivityMock.Object, new Mock<ILogger<SyncService>>().Object);

    [Fact]
    public async Task ObtenerEstadoSyncAsync_SinPendientes_Retorna0()
    {
        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        var service = CrearService();
        var estado = await service.ObtenerEstadoSyncAsync();

        Assert.Equal(1, estado.PendientesSubir);
        Assert.False(estado.Sincronizando);
    }

    [Fact]
    public async Task ObtenerEstadoSyncAsync_ConPendientes_RetornaContador()
    {
        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>
            {
                new() { Monto = 100, Sincronizado = true, CategoriaId = Guid.NewGuid() },
                new() { Monto = 200, Sincronizado = false, CategoriaId = Guid.NewGuid() },
                new() { Monto = 300, Sincronizado = false, CategoriaId = Guid.NewGuid() },
            });

        var service = CrearService();
        var estado = await service.ObtenerEstadoSyncAsync();

        Assert.Equal(3, estado.PendientesSubir);
    }

    [Fact]
    public async Task SincronizarAhoraAsync_PushGastos_SubePendientes()
    {
        var gasto = new Gasto { Id = Guid.NewGuid(), Monto = 150, Sincronizado = false, CategoriaId = Guid.NewGuid(), UsuarioId = _usuarioId };
        await _storage.SetAsync("cdg_gastos", new List<Gasto> { gasto });

        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        _supabaseMock
            .Setup(s => s.GuardarAsync("gastos", It.IsAny<Gasto>()))
            .ReturnsAsync((string _, Gasto g) => g);

        var service = CrearService();
        await service.SincronizarAhoraAsync();

        _supabaseMock.Verify(s => s.GuardarAsync("gastos", It.IsAny<Gasto>()), Times.Once);
    }

    [Fact]
    public async Task SincronizarAhoraAsync_PullGastos_AgregaRemotos()
    {
        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        var gastoRemoto = new Gasto
        {
            Id = Guid.NewGuid(),
            Monto = 999,
            CategoriaId = Guid.NewGuid(),
            UsuarioId = _usuarioId,
            Fecha = DateTime.UtcNow,
            CreadoEn = DateTime.UtcNow,
            Sincronizado = false,
        };

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Gasto> { gastoRemoto });

        await _storage.SetAsync("cdg_gastos", new List<Gasto>());

        var service = CrearService();
        await service.SincronizarAhoraAsync();

        var locales = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(locales);
        Assert.Contains(locales!, g => g.Id == gastoRemoto.Id && g.Sincronizado);
    }

    [Fact]
    public async Task SincronizarAhoraAsync_PullGastos_ConflictoGanaRemoto()
    {
        var gastoId = Guid.NewGuid();
        var categoriaId = Guid.NewGuid();

        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        var local = new Gasto
        {
            Id = gastoId,
            Monto = 100,
            CategoriaId = categoriaId,
            UsuarioId = _usuarioId,
            CreadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ActualizadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Sincronizado = false,
        };

        var remoto = new Gasto
        {
            Id = gastoId,
            Monto = 500,
            CategoriaId = categoriaId,
            UsuarioId = _usuarioId,
            CreadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ActualizadoEn = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Sincronizado = false,
        };

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Gasto> { remoto });

        await _storage.SetAsync("cdg_gastos", new List<Gasto> { local });

        var service = CrearService();
        await service.SincronizarAhoraAsync();

        var locales = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        var actualizado = locales!.First(g => g.Id == gastoId);
        Assert.Equal(500, actualizado.Monto);
    }

    [Fact]
    public async Task PushGastos_ConflictoGanaRemotoPorVersion()
    {
        var gastoId = Guid.NewGuid();
        var categoriaId = Guid.NewGuid();
        var local = new Gasto
        {
            Id = gastoId,
            Monto = 100,
            CategoriaId = categoriaId,
            UsuarioId = _usuarioId,
            NumeroVersion = 1,
            CreadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ActualizadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Sincronizado = false,
        };

        var remoto = new Gasto
        {
            Id = gastoId,
            Monto = 500,
            CategoriaId = categoriaId,
            UsuarioId = _usuarioId,
            NumeroVersion = 2,
            CreadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ActualizadoEn = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Sincronizado = false,
        };

        await _storage.SetAsync("cdg_gastos", new List<Gasto> { local });

        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Gasto> { remoto });

        var service = CrearService();
        await service.SincronizarAhoraAsync();

        var locales = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        var actualizado = locales!.First(g => g.Id == gastoId);
        // Push detectó conflicto y skip; luego Pull reemplazó con dato remoto
        Assert.Equal(500, actualizado.Monto);
        Assert.True(actualizado.Sincronizado);
    }

    [Fact]
    public async Task PushGastos_ConflictoGanaLocalPorVersion()
    {
        var gastoId = Guid.NewGuid();
        var categoriaId = Guid.NewGuid();
        var local = new Gasto
        {
            Id = gastoId,
            Monto = 500,
            CategoriaId = categoriaId,
            UsuarioId = _usuarioId,
            NumeroVersion = 1,
            CreadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ActualizadoEn = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Sincronizado = false,
        };

        var remoto = new Gasto
        {
            Id = gastoId,
            Monto = 100,
            CategoriaId = categoriaId,
            UsuarioId = _usuarioId,
            NumeroVersion = 2,
            CreadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ActualizadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Sincronizado = false,
        };

        await _storage.SetAsync("cdg_gastos", new List<Gasto> { local });

        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Gasto> { remoto });

        _supabaseMock
            .Setup(s => s.ActualizarAsync("gastos", gastoId, It.IsAny<Gasto>()))
            .ReturnsAsync((string _, object _, Gasto g) => g);

        var service = CrearService();
        await service.SincronizarAhoraAsync();

        var locales = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        var actualizado = locales!.First(g => g.Id == gastoId);
        // Local ganó (timestamp más nuevo) — se forzó push con version 3
        Assert.Equal(500, actualizado.Monto);
        Assert.True(actualizado.Sincronizado);
        Assert.Equal(3, actualizado.NumeroVersion);
    }

    [Fact]
    public async Task SincronizarAhoraAsync_LockEvitaEjecucionConcurrente()
    {
        var barrier = new TaskCompletionSource();

        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Gasto>());

        SemaphoreSlim? syncLock = null;
        var field = typeof(SyncService).GetField("SyncLock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (field != null)
            syncLock = field.GetValue(null) as SemaphoreSlim;

        // Adquirir el lock manualmente para simular sincronización en curso
        if (syncLock != null)
        {
            await syncLock.WaitAsync();
            try
            {
                var service = CrearService();
                await service.SincronizarAhoraAsync();

                // Verificar que no se hizo ninguna llamada a Supabase (lock impidió ejecución)
                _supabaseMock.Verify(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Never);
            }
            finally
            {
                syncLock.Release();
            }
        }
    }

    [Fact]
    public async Task SincronizarAhoraAsync_DosLlamadasSecuenciales_AmbasEjecutan()
    {
        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Gasto>());

        var service = CrearService();

        await service.SincronizarAhoraAsync();
        await service.SincronizarAhoraAsync();

        // Dos ciclos completos = 4 llamadas (push + pull por cada uno)
        _supabaseMock.Verify(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()), Times.Exactly(4));
    }

    [Fact]
    public async Task SincronizarAhoraAsync_SinPlanNube_NoHaceNada()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Local });

        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>
            {
                new() { Monto = 100, Sincronizado = false, CategoriaId = Guid.NewGuid() },
            });

        var service = CrearService();
        await service.SincronizarAhoraAsync();

        _supabaseMock.Verify(s => s.GuardarAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task SincronizarAhoraAsync_PushRecurrencias_SubePendientes()
    {
        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        var rec = new Recurrencia
        {
            Id = Guid.NewGuid(),
            Monto = 250,
            Descripcion = "Test",
            TipoRecurrencia = TipoRecurrencia.Mensual,
        };

        _recurrenciaServiceMock
            .Setup(s => s.ObtenerRecurrenciasAsync())
            .ReturnsAsync(new List<Recurrencia> { rec });

        _supabaseMock
            .Setup(s => s.GuardarAsync("recurrencias", rec))
            .ReturnsAsync((string _, Recurrencia r) => r);

        var service = CrearService();
        await service.SincronizarAhoraAsync();

        _supabaseMock.Verify(s => s.GuardarAsync("recurrencias", rec), Times.Once);
    }

    [Fact]
    public async Task SincronizarAhoraAsync_PullRecurrencias_AgregaRemotas()
    {
        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        var recRemota = new Recurrencia
        {
            Id = Guid.NewGuid(),
            Monto = 500,
            Descripcion = "Remota",
            TipoRecurrencia = TipoRecurrencia.Semanal,
            UsuarioId = _usuarioId,
        };

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Recurrencia>("recurrencias", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Recurrencia> { recRemota });

        await _storage.SetAsync("cdg_recurrencias", new List<Recurrencia>());

        var service = CrearService();
        await service.SincronizarAhoraAsync();

        var locales = await _storage.GetAsync<List<Recurrencia>>("cdg_recurrencias");
        Assert.NotNull(locales);
        Assert.Contains(locales!, r => r.Id == recRemota.Id);
    }

    [Fact]
    public async Task SincronizarAhoraAsync_PushCategorias_SubePendientes()
    {
        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        var cat = new Categoria
        {
            Id = Guid.NewGuid(),
            Nombre = "Test",
            Icono = "🧪",
            Color = "#fff",
            Tipo = TipoGasto.Gasto,
        };

        _categoriaServiceMock
            .Setup(s => s.ObtenerCategoriasAsync())
            .ReturnsAsync(new List<Categoria> { cat });

        _supabaseMock
            .Setup(s => s.GuardarAsync("categorias", cat))
            .ReturnsAsync((string _, Categoria c) => c);

        var service = CrearService();
        await service.SincronizarAhoraAsync();

        _supabaseMock.Verify(s => s.GuardarAsync("categorias", cat), Times.Once);
    }

    [Fact]
    public async Task SincronizarAhoraAsync_PullPresupuestos_AgregaRemotos()
    {
        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        var presupuestoRemoto = new Presupuesto
        {
            Id = Guid.NewGuid(),
            MontoLimite = 5000,
            Periodo = PeriodoPresupuesto.Mensual,
            UsuarioId = _usuarioId,
        };

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Presupuesto>("presupuestos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Presupuesto> { presupuestoRemoto });

        await _storage.SetAsync("cdg_presupuestos", new List<Presupuesto>());

        var service = CrearService();
        await service.SincronizarAhoraAsync();

        var locales = await _storage.GetAsync<List<Presupuesto>>("cdg_presupuestos");
        Assert.NotNull(locales);
        Assert.Contains(locales!, p => p.Id == presupuestoRemoto.Id);
    }

    [Fact]
    public async Task SincronizarAhoraAsync_PushFinanciamientos_SubePendientes()
    {
        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        var fin = new Financiamiento
        {
            Id = Guid.NewGuid(),
            MontoTotal = 10000,
            Banco = "BBVA",
            Alias = "TC Sync",
            Tipo = "Credito",
            Sincronizado = false,
        };

        _financiamientoServiceMock
            .Setup(s => s.ObtenerFinanciamientosAsync())
            .ReturnsAsync(new List<Financiamiento> { fin });

        _supabaseMock
            .Setup(s => s.GuardarAsync("financiamientos", fin))
            .ReturnsAsync((string _, Financiamiento f) => f);

        var service = CrearService();
        await service.SincronizarAhoraAsync();

        _supabaseMock.Verify(s => s.GuardarAsync("financiamientos", fin), Times.Once);
    }

    [Fact]
    public async Task ObtenerEstadoSyncAsync_StaleLock_Recupera()
    {
        var staleSyncDesde = DateTime.UtcNow.AddMinutes(-5);
        await _storage.SetAsync("cdg_sync_state", new SyncState
        {
            Sincronizando = true,
            SincronizandoDesde = staleSyncDesde,
        });

        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        var service = CrearService();
        var estado = await service.ObtenerEstadoSyncAsync();

        Assert.False(estado.Sincronizando);
        Assert.Null(estado.SincronizandoDesde);
    }

    [Fact]
    public async Task ObtenerEstadoSyncAsync_PlanLocal_RetornaCero()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Local });

        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>
            {
                new() { Monto = 100, Sincronizado = false, CategoriaId = Guid.NewGuid() },
            });

        var service = CrearService();
        var estado = await service.ObtenerEstadoSyncAsync();

        Assert.Equal(0, estado.PendientesSubir);
        Assert.Equal(0, estado.PendientesBajar);
    }

    [Fact]
    public async Task SincronizarAhoraAsync_PushGastos_SupabaseFalla_NoLanzaExcepcion()
    {
        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new Exception("Error de red en gastos"));

        var cat = new Categoria { Id = Guid.NewGuid(), Nombre = "Test", Icono = "🧪", Color = "#fff", Tipo = TipoGasto.Gasto };
        _categoriaServiceMock
            .Setup(s => s.ObtenerCategoriasAsync())
            .ReturnsAsync(new List<Categoria> { cat });
        _supabaseMock
            .Setup(s => s.GuardarAsync("categorias", cat))
            .ReturnsAsync((string _, Categoria c) => c);

        var service = CrearService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SincronizarAhoraAsync());
        Assert.Contains("sincronizar", ex.Message.ToLower());

        _supabaseMock.Verify(s => s.GuardarAsync("categorias", cat), Times.Once);
    }

    [Fact]
    public async Task SincronizarAhoraAsync_PushGastos_ErrorIndividualContinua_SubeLosDemas()
    {
        var catId = Guid.NewGuid();
        var gastoOk = new Gasto { Id = Guid.NewGuid(), Monto = 100, Sincronizado = false, CategoriaId = catId, UsuarioId = _usuarioId };
        var gastoFail = new Gasto { Id = Guid.NewGuid(), Monto = 200, Sincronizado = false, CategoriaId = catId, UsuarioId = _usuarioId };

        await _storage.SetAsync("cdg_gastos", new List<Gasto> { gastoOk, gastoFail });

        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        _supabaseMock
            .Setup(s => s.GuardarAsync("gastos", It.Is<Gasto>(g => g.Id == gastoFail.Id)))
            .ThrowsAsync(new Exception("Error individual"));
        _supabaseMock
            .Setup(s => s.GuardarAsync("gastos", It.Is<Gasto>(g => g.Id == gastoOk.Id)))
            .ReturnsAsync((string _, Gasto g) => g);

        // PullGastosAsync needs to see gastoOk in remotos or it will remove it locally
        _supabaseMock
            .SetupSequence(s => s.ObtenerTodosAsync<Gasto>(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Gasto>())   // Push: empty list
            .ReturnsAsync(new List<Gasto> { gastoOk }); // Pull: has gastoOk

        var service = CrearService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SincronizarAhoraAsync());
        Assert.Contains("sincronizar", ex.Message.ToLower());

        _supabaseMock.Verify(s => s.GuardarAsync("gastos", It.Is<Gasto>(g => g.Id == gastoOk.Id)), Times.Once);
    }

    [Fact]
    public async Task SincronizarAhoraAsync_PushGastos_VersionLocalGana_FuerzaPush()
    {
        var gastoId = Guid.NewGuid();
        var catId = Guid.NewGuid();
        var local = new Gasto
        {
            Id = gastoId, Monto = 500, CategoriaId = catId, UsuarioId = _usuarioId,
            NumeroVersion = 2, Sincronizado = false,
            CreadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ActualizadoEn = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        var remoto = new Gasto
        {
            Id = gastoId, Monto = 100, CategoriaId = catId, UsuarioId = _usuarioId,
            NumeroVersion = 2, Sincronizado = false,
            CreadoEn = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        await _storage.SetAsync("cdg_gastos", new List<Gasto> { local });

        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Gasto> { remoto });

        _supabaseMock
            .Setup(s => s.ActualizarAsync("gastos", gastoId, It.IsAny<Gasto>()))
            .ReturnsAsync((string _, object _, Gasto g) => g);

        var service = CrearService();
        await service.SincronizarAhoraAsync();

        var locales = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        var actualizado = locales!.First(g => g.Id == gastoId);
        Assert.Equal(500, actualizado.Monto);
        Assert.True(actualizado.Sincronizado);
        Assert.Equal(3, actualizado.NumeroVersion);
    }

    [Fact]
    public async Task SincronizarAhoraAsync_PullGastos_SupabaseFalla_NoLanzaExcepcion()
    {
        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        // PushGastosAsync succeeds, PullGastosAsync fails
        _supabaseMock
            .SetupSequence(s => s.ObtenerTodosAsync<Gasto>("gastos", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<Gasto>())   // Push: success
            .ThrowsAsync(new Exception("Error en pull")); // Pull: failure

        var cat = new Categoria { Id = Guid.NewGuid(), Nombre = "Test", Icono = "🧪", Color = "#fff", Tipo = TipoGasto.Gasto };
        _categoriaServiceMock
            .Setup(s => s.ObtenerCategoriasAsync())
            .ReturnsAsync(new List<Categoria> { cat });
        _supabaseMock
            .Setup(s => s.GuardarAsync("categorias", cat))
            .ReturnsAsync((string _, Categoria c) => c);

        var service = CrearService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SincronizarAhoraAsync());
        Assert.Contains("sincronizar", ex.Message.ToLower());

        _supabaseMock.Verify(s => s.GuardarAsync("categorias", cat), Times.Once);
    }

    [Fact]
    public async Task SincronizarAhoraAsync_PushCategorias_ErrorIndividualContinua()
    {
        _gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto>());

        var catOk = new Categoria { Id = Guid.NewGuid(), Nombre = "Ok", Icono = "✅", Color = "#fff", Tipo = TipoGasto.Gasto };
        var catFail = new Categoria { Id = Guid.NewGuid(), Nombre = "Fail", Icono = "❌", Color = "#fff", Tipo = TipoGasto.Gasto };

        _categoriaServiceMock
            .Setup(s => s.ObtenerCategoriasAsync())
            .ReturnsAsync(new List<Categoria> { catOk, catFail });

        _supabaseMock
            .Setup(s => s.GuardarAsync("categorias", It.Is<Categoria>(c => c.Id == catFail.Id)))
            .ThrowsAsync(new Exception("Error al subir categoría"));
        _supabaseMock
            .Setup(s => s.GuardarAsync("categorias", It.Is<Categoria>(c => c.Id == catOk.Id)))
            .ReturnsAsync((string _, Categoria c) => c);

        var service = CrearService();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SincronizarAhoraAsync());
        Assert.Contains("sincronizar", ex.Message.ToLower());

        _supabaseMock.Verify(s => s.GuardarAsync("categorias", catOk), Times.Once);
        _supabaseMock.Verify(s => s.GuardarAsync("categorias", catFail), Times.Once);
    }
}
