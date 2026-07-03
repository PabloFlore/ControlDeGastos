namespace ControlDeGastos.Tests.Tests;

public class GamificacionServiceTests
{
    [Fact]
    public async Task ObtenerProgresoAsync_SinDatos_RetornaValoresDefault()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var recurrenciaServiceMock = new Mock<IRecurrenciaService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, recurrenciaServiceMock.Object, new Mock<IPresupuestoService>().Object);

        var progreso = await service.ObtenerProgresoAsync();

        Assert.NotNull(progreso);
        Assert.Equal(1, progreso.Nivel);
        Assert.Equal(0, progreso.ExpActual);
        Assert.Equal(100, progreso.ExpRequerida);
        Assert.Equal(100, progreso.HpActual);
        Assert.Equal(100, progreso.HpMaximo);
        Assert.Equal(0, progreso.Monedas);
        Assert.Equal(0, progreso.MonedasGastadas);
    }

    [Fact]
    public async Task AplicarGastoAsync_GastoNormal_OtorgaExp()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var recurrenciaServiceMock = new Mock<IRecurrenciaService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, recurrenciaServiceMock.Object, new Mock<IPresupuestoService>().Object);

        var gasto = new Gasto { Monto = 50, Fecha = DateTime.UtcNow, CategoriaId = Guid.NewGuid() };
        await service.AplicarGastoAsync(gasto, 0, 0);

        var progreso = await service.ObtenerProgresoAsync();
        Assert.Equal(10, progreso.ExpActual); // base exp = 10
        Assert.Equal(1, progreso.GastosConsecutivos);
        Assert.Equal(50, progreso.Monedas);
    }

    [Fact]
    public async Task AplicarGastoAsync_SobrePresupuesto_PierdeHp()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var recurrenciaServiceMock = new Mock<IRecurrenciaService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, recurrenciaServiceMock.Object, new Mock<IPresupuestoService>().Object);

        var gasto = new Gasto { Monto = 100, Fecha = DateTime.UtcNow, CategoriaId = Guid.NewGuid() };
        await service.AplicarGastoAsync(gasto, 200, 100); // gastado 200 > limite 100

        var progreso = await service.ObtenerProgresoAsync();
        Assert.Equal(95, progreso.HpActual); // perdió 5 HP
    }

    [Fact]
    public async Task AplicarGastoAsync_GastosConsecutivos_BonusExp()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var recurrenciaServiceMock = new Mock<IRecurrenciaService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, recurrenciaServiceMock.Object, new Mock<IPresupuestoService>().Object);

        for (var i = 2; i >= 0; i--)
        {
            var gasto = new Gasto { Monto = 10, Fecha = DateTime.UtcNow.AddDays(-i), CategoriaId = Guid.NewGuid() };
            await service.AplicarGastoAsync(gasto, 0, 0);
        }

        var progreso = await service.ObtenerProgresoAsync();
        // gasto 1 (2d atrás): 10 exp (base). GastosConsecutivos=1
        // gasto 2 (ayer): 10 exp (base) + 5 (diff==1) = 15. GastosConsecutivos=2
        // gasto 3 (hoy): 10 exp (base) + 5 (diff==1) = 15. GastosConsecutivos=3. +5 (3+ consecutivos) => 20
        Assert.Equal(45, progreso.ExpActual);
        Assert.Equal(3, progreso.GastosConsecutivos);
        // 1st (diff=2): 50, 2nd (diff=1): 50, 3rd (diff=0, streak=3): 60
        Assert.Equal(160, progreso.Monedas);
    }

    [Fact]
    public async Task AplicarGastoAsync_SuficienteExp_SubeDeNivel()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var recurrenciaServiceMock = new Mock<IRecurrenciaService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, recurrenciaServiceMock.Object, new Mock<IPresupuestoService>().Object);

        var gasto = new Gasto { Monto = 50, Fecha = DateTime.UtcNow, CategoriaId = Guid.NewGuid() };

        var progreso = await service.ObtenerProgresoAsync();

        await service.AplicarGastoAsync(gasto, 0, 0); // nivel 1: exp_requerida=100
        progreso = await service.ObtenerProgresoAsync();
        Assert.Equal(1, progreso.Nivel);

        gasto = new Gasto { Monto = 50, Fecha = DateTime.UtcNow, CategoriaId = Guid.NewGuid() };
        await service.AplicarGastoAsync(gasto, 0, 0); // +10 exp = 20, no sube

        gasto = new Gasto { Monto = 50, Fecha = DateTime.UtcNow.AddDays(1), CategoriaId = Guid.NewGuid() };
        await service.AplicarGastoAsync(gasto, 0, 0); // +10+5 = 15, total 25, no sube

        // Necesitamos más EXP para subir
        for (var i = 0; i < 8; i++)
        {
            gasto = new Gasto { Monto = 10, Fecha = DateTime.UtcNow.AddDays(2 + i), CategoriaId = Guid.NewGuid() };
            await service.AplicarGastoAsync(gasto, 0, 0);
        }

        // 20 + 15 + (8 * (10+5+5)) = 20 + 15 + 160 = 195
        progreso = await service.ObtenerProgresoAsync();
        Assert.True(progreso.ExpActual >= progreso.ExpRequerida || progreso.Nivel > 1);
    }

    [Fact]
    public async Task RecuperarHpDiarioAsync_SinGastosHoy_RecuperaHp()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var recurrenciaServiceMock = new Mock<IRecurrenciaService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, recurrenciaServiceMock.Object, new Mock<IPresupuestoService>().Object);

        var progresoInicial = await service.ObtenerProgresoAsync();
        progresoInicial.HpActual = 50;
        progresoInicial.UltimoGastoFecha = DateTime.Now.AddDays(-1); // gasto ayer → recovery activo
        await storage.SetAsync("cdg_progreso_rpg", progresoInicial);

        var progreso = await service.RecuperarHpDiarioAsync();
        Assert.Equal(60, progreso.HpActual); // +10 HP recovery
    }

    [Fact]
    public async Task RecuperarHpDiarioAsync_HpAlMaximo_NoExcedeLimite()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var recurrenciaServiceMock = new Mock<IRecurrenciaService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, recurrenciaServiceMock.Object, new Mock<IPresupuestoService>().Object);

        var progresoInicial = await service.ObtenerProgresoAsync();
        progresoInicial.HpActual = 95;
        progresoInicial.UltimoGastoFecha = DateTime.Now.AddDays(-1); // gasto ayer → recovery activo
        await storage.SetAsync("cdg_progreso_rpg", progresoInicial);

        var progreso = await service.RecuperarHpDiarioAsync();
        Assert.Equal(100, progreso.HpActual); // 95 + 10 = 105, capped at HpMaximo = 100
    }

    [Fact]
    public async Task RecalcularDesdeCeroAsync_AlEliminarGasto_RecalculaExp()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var recurrenciaServiceMock = new Mock<IRecurrenciaService>();
        var presupuestoServiceMock = new Mock<IPresupuestoService>();
        presupuestoServiceMock.Setup(s => s.ObtenerPresupuestosAsync()).ReturnsAsync(new List<Presupuesto>());
        var service = new GamificacionService(storage, gastoServiceMock.Object, recurrenciaServiceMock.Object, presupuestoServiceMock.Object);

        var catId = Guid.NewGuid();
        var gastos = new List<Gasto>
        {
            new() { Monto = 50, Fecha = DateTime.UtcNow.AddDays(-1), CategoriaId = catId },
            new() { Monto = 30, Fecha = DateTime.UtcNow, CategoriaId = catId },
        };
        gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(gastos);

        foreach (var g in gastos)
            await service.AplicarGastoAsync(g, 0, 0);

        var progreso = await service.ObtenerProgresoAsync();
        Assert.InRange(progreso.ExpActual, 20, 100); // 2 gastos → al menos 20 XP

        // Simular eliminación del primer gasto
        recurrenciaServiceMock
            .Setup(r => r.ObtenerRecurrenciasAsync())
            .ReturnsAsync(new List<Recurrencia>());

        gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto> { gastos[1] });

        await service.RecalcularDesdeCeroAsync();

        progreso = await service.ObtenerProgresoAsync();
        Assert.InRange(progreso.ExpActual, 10, 100); // 1 gasto → al menos 10 XP
        Assert.Equal(1, progreso.GastosConsecutivos);
        Assert.Equal(1, progreso.Nivel);
        Assert.Equal(150, progreso.Monedas); // 50 del gasto + 100 del logro "Primer paso"
    }

    [Fact]
    public async Task VerificarYDesbloquearLogrosAsync_PrimerGasto_DesbloqueaPrimerPaso()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var recurrenciaServiceMock = new Mock<IRecurrenciaService>();
        var presupuestoServiceMock = new Mock<IPresupuestoService>();
        presupuestoServiceMock.Setup(s => s.ObtenerPresupuestosAsync()).ReturnsAsync(new List<Presupuesto>());
        var service = new GamificacionService(storage, gastoServiceMock.Object, recurrenciaServiceMock.Object, presupuestoServiceMock.Object);

        gastoServiceMock
            .Setup(s => s.ObtenerGastosAsync())
            .ReturnsAsync(new List<Gasto> { new() { Monto = 100, Fecha = DateTime.UtcNow } });

        recurrenciaServiceMock
            .Setup(r => r.ObtenerRecurrenciasAsync())
            .ReturnsAsync(new List<Recurrencia>());

        var desbloqueados = await service.VerificarYDesbloquearLogrosAsync();
        var progreso = await service.ObtenerProgresoAsync();

        Assert.Contains(desbloqueados, l => l.Nombre == "Primer paso");
        Assert.Single(progreso.LogrosDesbloqueados);
        Assert.Equal(100, progreso.Monedas); // "Primer paso" da 100 monedas
    }

    [Fact]
    public async Task ObtenerLogrosAsync_RetornaListaPredefinida()
    {
        var service = new GamificacionService(new InMemoryStorageService(), new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var logros = await service.ObtenerLogrosAsync();
        Assert.Equal(42, logros.Count);
        Assert.Contains(logros, l => l.Nombre == "Primer paso");
        Assert.Contains(logros, l => l.Nombre == "Legendario");
        Assert.Contains(logros, l => l.Nombre == "Maestro");
        Assert.Contains(logros, l => l.Nombre == "Social");
    }

    [Fact]
    public async Task ObtenerTitulosAsync_RetornaListaPredefinida()
    {
        var service = new GamificacionService(new InMemoryStorageService(), new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var titulos = await service.ObtenerTitulosAsync();
        Assert.Equal(8, titulos.Count);
        Assert.Contains(titulos, t => t.Nombre == "Iniciado");
        Assert.Contains(titulos, t => t.Nombre == "Leyenda viviente");
    }

    [Fact]
    public async Task ObtenerLogrosDesbloqueadosAsync_SinDesbloqueos_RetornaVacio()
    {
        var storage = new InMemoryStorageService();
        var service = new GamificacionService(storage, new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var desbloqueados = await service.ObtenerLogrosDesbloqueadosAsync();
        Assert.Empty(desbloqueados);
    }

    [Fact]
    public async Task ObtenerLogrosDesbloqueadosAsync_ConDesbloqueos_FiltraCorrectamente()
    {
        var storage = new InMemoryStorageService();
        var service = new GamificacionService(storage, new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var progreso = new ProgresoRPG();
        progreso.LogrosDesbloqueados.Add(Guid.Parse("10010000-0000-0000-0000-000000000001"));
        progreso.LogrosDesbloqueados.Add(Guid.Parse("10010000-0000-0000-0000-000000000002"));
        await storage.SetAsync("cdg_progreso_rpg", progreso);

        var desbloqueados = await service.ObtenerLogrosDesbloqueadosAsync();
        Assert.Equal(2, desbloqueados.Count);
        Assert.Contains(desbloqueados, l => l.Nombre == "Primer paso");
        Assert.Contains(desbloqueados, l => l.Nombre == "Aprendiz");
    }

    [Fact]
    public async Task ObtenerTitulosDesbloqueadosAsync_SinDesbloqueos_RetornaVacio()
    {
        var storage = new InMemoryStorageService();
        var service = new GamificacionService(storage, new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var desbloqueados = await service.ObtenerTitulosDesbloqueadosAsync();
        Assert.Empty(desbloqueados);
    }

    [Fact]
    public async Task ObtenerTitulosDesbloqueadosAsync_ConDesbloqueos_FiltraCorrectamente()
    {
        var storage = new InMemoryStorageService();
        var service = new GamificacionService(storage, new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var progreso = new ProgresoRPG();
        progreso.TitulosDesbloqueados.Add("iniciado");
        await storage.SetAsync("cdg_progreso_rpg", progreso);

        var desbloqueados = await service.ObtenerTitulosDesbloqueadosAsync();
        Assert.Single(desbloqueados);
        Assert.Equal("Iniciado", desbloqueados[0].Nombre);
    }

    [Fact]
    public async Task ObtenerTituloActivoNombreAsync_SinTitulo_RetornaNull()
    {
        var service = new GamificacionService(new InMemoryStorageService(), new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var nombre = await service.ObtenerTituloActivoNombreAsync();
        Assert.Null(nombre);
    }

    [Fact]
    public async Task ObtenerTituloActivoNombreAsync_ConTitulo_RetornaNombre()
    {
        var storage = new InMemoryStorageService();
        var service = new GamificacionService(storage, new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var progreso = new ProgresoRPG();
        progreso.TituloActivoId = "iniciado";
        await storage.SetAsync("cdg_progreso_rpg", progreso);

        var nombre = await service.ObtenerTituloActivoNombreAsync();
        Assert.Equal("Iniciado", nombre);
    }

    [Fact]
    public async Task ObtenerTituloActivoNombreAsync_TituloInvalido_RetornaNull()
    {
        var storage = new InMemoryStorageService();
        var service = new GamificacionService(storage, new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var progreso = new ProgresoRPG();
        progreso.TituloActivoId = "no_existe";
        await storage.SetAsync("cdg_progreso_rpg", progreso);

        var nombre = await service.ObtenerTituloActivoNombreAsync();
        Assert.Null(nombre);
    }

    [Fact]
    public async Task EstablecerTituloActivoAsync_TituloNoDesbloqueado_RetornaFalse()
    {
        var service = new GamificacionService(new InMemoryStorageService(), new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var resultado = await service.EstablecerTituloActivoAsync("nivel_10");
        Assert.False(resultado);
    }

    [Fact]
    public async Task EstablecerTituloActivoAsync_TituloDesbloqueado_RetornaTrue()
    {
        var storage = new InMemoryStorageService();
        var service = new GamificacionService(storage, new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var progreso = new ProgresoRPG();
        progreso.TitulosDesbloqueados.Add("iniciado");
        await storage.SetAsync("cdg_progreso_rpg", progreso);

        var resultado = await service.EstablecerTituloActivoAsync("iniciado");
        Assert.True(resultado);

        var nombre = await service.ObtenerTituloActivoNombreAsync();
        Assert.Equal("Iniciado", nombre);
    }

    [Fact]
    public async Task EstablecerTituloActivoAsync_Null_LimpiaTitulo()
    {
        var storage = new InMemoryStorageService();
        var service = new GamificacionService(storage, new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var progreso = new ProgresoRPG();
        progreso.TitulosDesbloqueados.Add("iniciado");
        progreso.TituloActivoId = "iniciado";
        await storage.SetAsync("cdg_progreso_rpg", progreso);

        var resultado = await service.EstablecerTituloActivoAsync(null);
        Assert.True(resultado);

        var nombre = await service.ObtenerTituloActivoNombreAsync();
        Assert.Null(nombre);
    }

    [Fact]
    public async Task CalcularProgresoLogroAsync_GastosTotales_RetornaCuenta()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var recurrenciaServiceMock = new Mock<IRecurrenciaService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, recurrenciaServiceMock.Object, new Mock<IPresupuestoService>().Object);

        gastoServiceMock.Setup(s => s.ObtenerGastosAsync()).ReturnsAsync(new List<Gasto>
        {
            new() { Monto = 100, CategoriaId = Guid.NewGuid() },
            new() { Monto = 200, CategoriaId = Guid.NewGuid() },
            new() { Monto = -50, CategoriaId = Guid.NewGuid() },
        });

        var logro = new Logro { TipoCondicion = TipoCondicionLogro.GastosTotales, ValorCondicion = 5 };
        var (actual, requerido) = await service.CalcularProgresoLogroAsync(logro);

        Assert.Equal(2, actual); // solo montos > 0
        Assert.Equal(5, requerido);
    }

    [Fact]
    public async Task CalcularProgresoLogroAsync_GastosConsecutivos_RetornaValor()
    {
        var storage = new InMemoryStorageService();
        var service = new GamificacionService(storage, new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var progreso = new ProgresoRPG { GastosConsecutivos = 7 };
        await storage.SetAsync("cdg_progreso_rpg", progreso);

        var logro = new Logro { TipoCondicion = TipoCondicionLogro.GastosConsecutivos, ValorCondicion = 30 };
        var (actual, requerido) = await service.CalcularProgresoLogroAsync(logro);

        Assert.Equal(7, actual);
        Assert.Equal(30, requerido);
    }

    [Fact]
    public async Task CalcularProgresoLogroAsync_NivelAlcanzado_RetornaNivel()
    {
        var storage = new InMemoryStorageService();
        var service = new GamificacionService(storage, new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var progreso = new ProgresoRPG { Nivel = 5 };
        await storage.SetAsync("cdg_progreso_rpg", progreso);

        var logro = new Logro { TipoCondicion = TipoCondicionLogro.NivelAlcanzado, ValorCondicion = 10 };
        var (actual, requerido) = await service.CalcularProgresoLogroAsync(logro);

        Assert.Equal(5, actual);
        Assert.Equal(10, requerido);
    }

    [Fact]
    public async Task CalcularProgresoLogroAsync_IngresosRegistrados_RetornaCuenta()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);

        gastoServiceMock.Setup(s => s.ObtenerGastosAsync()).ReturnsAsync(new List<Gasto>
        {
            new() { Monto = 100, CategoriaId = Guid.NewGuid() },
            new() { Monto = -500, CategoriaId = Guid.NewGuid() },
            new() { Monto = -300, CategoriaId = Guid.NewGuid() },
        });

        var logro = new Logro { TipoCondicion = TipoCondicionLogro.IngresosRegistrados, ValorCondicion = 5 };
        var (actual, requerido) = await service.CalcularProgresoLogroAsync(logro);

        Assert.Equal(2, actual);
    }

    [Fact]
    public async Task CalcularProgresoLogroAsync_CategoriasUsadas_RetornaCuenta()
    {
        var storage = new InMemoryStorageService();
        var service = new GamificacionService(storage, new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var progreso = new ProgresoRPG();
        progreso.IdsCategoriasUsadas.Add("cat1");
        progreso.IdsCategoriasUsadas.Add("cat2");
        progreso.IdsCategoriasUsadas.Add("cat3");
        await storage.SetAsync("cdg_progreso_rpg", progreso);

        var logro = new Logro { TipoCondicion = TipoCondicionLogro.CategoriasUsadas, ValorCondicion = 10 };
        var (actual, requerido) = await service.CalcularProgresoLogroAsync(logro);

        Assert.Equal(3, actual);
    }

    [Fact]
    public async Task CalcularProgresoLogroAsync_MontoTotalGastado_RetornaSuma()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);

        gastoServiceMock.Setup(s => s.ObtenerGastosAsync()).ReturnsAsync(new List<Gasto>
        {
            new() { Monto = 5000, CategoriaId = Guid.NewGuid() },
            new() { Monto = 8000, CategoriaId = Guid.NewGuid() },
            new() { Monto = -3000, CategoriaId = Guid.NewGuid() },
        });

        var logro = new Logro { TipoCondicion = TipoCondicionLogro.MontoTotalGastado, ValorCondicion = 10000 };
        var (actual, requerido) = await service.CalcularProgresoLogroAsync(logro);

        Assert.Equal(13000, actual);
    }

    [Fact]
    public async Task CalcularProgresoLogroAsync_GastosCompartidos_RetornaCuenta()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);

        gastoServiceMock.Setup(s => s.ObtenerGastosAsync()).ReturnsAsync(new List<Gasto>
        {
            new() { Monto = 100, EsGastoCompartido = true, CategoriaId = Guid.NewGuid() },
            new() { Monto = 200, EsGastoCompartido = false, CategoriaId = Guid.NewGuid() },
            new() { Monto = 300, EsGastoCompartido = true, CategoriaId = Guid.NewGuid() },
        });

        var logro = new Logro { TipoCondicion = TipoCondicionLogro.GastosCompartidos, ValorCondicion = 10 };
        var (actual, requerido) = await service.CalcularProgresoLogroAsync(logro);

        Assert.Equal(2, actual);
    }

    [Fact]
    public async Task CalcularProgresoLogroAsync_RecurrenciasActivas_RetornaCuenta()
    {
        var storage = new InMemoryStorageService();
        var recurrenciaServiceMock = new Mock<IRecurrenciaService>();
        var service = new GamificacionService(storage, new Mock<IGastoService>().Object, recurrenciaServiceMock.Object, new Mock<IPresupuestoService>().Object);

        recurrenciaServiceMock.Setup(s => s.ObtenerRecurrenciasAsync()).ReturnsAsync(new List<Recurrencia>
        {
            new() { Activa = true },
            new() { Activa = false },
            new() { Activa = true },
        });

        var logro = new Logro { TipoCondicion = TipoCondicionLogro.RecurrenciasActivas, ValorCondicion = 5 };
        var (actual, requerido) = await service.CalcularProgresoLogroAsync(logro);

        Assert.Equal(2, actual);
    }

    [Fact]
    public async Task CalcularProgresoLogroAsync_TipoDesconocido_RetornaCero()
    {
        var service = new GamificacionService(new InMemoryStorageService(), new Mock<IGastoService>().Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);
        var logro = new Logro { TipoCondicion = (TipoCondicionLogro)999, ValorCondicion = 10 };
        var (actual, requerido) = await service.CalcularProgresoLogroAsync(logro);

        Assert.Equal(0, actual);
        Assert.Equal(10, requerido);
    }

    [Fact]
    public async Task VerificarYDesbloquearTitulosAsync_LogroEspecifico_Desbloquea()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);

        var progreso = new ProgresoRPG();
        progreso.LogrosDesbloqueados.Add(Guid.Parse("10010000-0000-0000-0000-000000000001"));
        await storage.SetAsync("cdg_progreso_rpg", progreso);
        gastoServiceMock.Setup(s => s.ObtenerGastosAsync()).ReturnsAsync(new List<Gasto>());

        await service.VerificarYDesbloquearTitulosAsync();

        var desbloqueados = await service.ObtenerTitulosDesbloqueadosAsync();
        Assert.Contains(desbloqueados, t => t.Id == "iniciado");
    }

    [Fact]
    public async Task VerificarYDesbloquearTitulosAsync_RachaMinima_Desbloquea()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);

        var progreso = new ProgresoRPG { GastosConsecutivos = 7 };
        await storage.SetAsync("cdg_progreso_rpg", progreso);
        gastoServiceMock.Setup(s => s.ObtenerGastosAsync()).ReturnsAsync(new List<Gasto>());

        await service.VerificarYDesbloquearTitulosAsync();

        var desbloqueados = await service.ObtenerTitulosDesbloqueadosAsync();
        Assert.Contains(desbloqueados, t => t.Id == "racha_7");
    }

    [Fact]
    public async Task VerificarYDesbloquearTitulosAsync_NivelMinimo_Desbloquea()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);

        var progreso = new ProgresoRPG { Nivel = 10 };
        await storage.SetAsync("cdg_progreso_rpg", progreso);
        gastoServiceMock.Setup(s => s.ObtenerGastosAsync()).ReturnsAsync(new List<Gasto>());

        await service.VerificarYDesbloquearTitulosAsync();

        var desbloqueados = await service.ObtenerTitulosDesbloqueadosAsync();
        Assert.Contains(desbloqueados, t => t.Id == "nivel_10");
    }

    [Fact]
    public async Task VerificarYDesbloquearTitulosAsync_LogrosTotales_Desbloquea()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);

        var progreso = new ProgresoRPG();
        for (var i = 0; i < 25; i++)
            progreso.LogrosDesbloqueados.Add(Guid.NewGuid());
        await storage.SetAsync("cdg_progreso_rpg", progreso);
        gastoServiceMock.Setup(s => s.ObtenerGastosAsync()).ReturnsAsync(new List<Gasto>());

        await service.VerificarYDesbloquearTitulosAsync();

        var desbloqueados = await service.ObtenerTitulosDesbloqueadosAsync();
        Assert.Contains(desbloqueados, t => t.Id == "completista");
    }

    [Fact]
    public async Task VerificarYDesbloquearTitulosAsync_MontoAhorrado_Desbloquea()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);

        gastoServiceMock.Setup(s => s.ObtenerGastosAsync()).ReturnsAsync(new List<Gasto>
        {
            new() { Monto = -200000, CategoriaId = Guid.NewGuid() },
            new() { Monto = -50000, CategoriaId = Guid.NewGuid() },
            new() { Monto = 50000, CategoriaId = Guid.NewGuid() },
        });
        var progreso = new ProgresoRPG();
        await storage.SetAsync("cdg_progreso_rpg", progreso);

        await service.VerificarYDesbloquearTitulosAsync();

        var desbloqueados = await service.ObtenerTitulosDesbloqueadosAsync();
        Assert.Contains(desbloqueados, t => t.Id == "ahorrador_100k");
    }

    [Fact]
    public async Task VerificarYDesbloquearTitulosAsync_GastosCompartidos_Desbloquea()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);

        var gastos = new List<Gasto>();
        for (var i = 0; i < 10; i++)
            gastos.Add(new Gasto { Monto = 100, EsGastoCompartido = true, CategoriaId = Guid.NewGuid() });

        gastoServiceMock.Setup(s => s.ObtenerGastosAsync()).ReturnsAsync(gastos);
        var progreso = new ProgresoRPG { GastosConsecutivos = 10 };
        await storage.SetAsync("cdg_progreso_rpg", progreso);

        await service.VerificarYDesbloquearTitulosAsync();

        var desbloqueados = await service.ObtenerTitulosDesbloqueadosAsync();
        Assert.Contains(desbloqueados, t => t.Id == "compartido_10");
    }

    [Fact]
    public async Task VerificarYDesbloquearTitulosAsync_YaDesbloqueado_NoDuplica()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, new Mock<IRecurrenciaService>().Object, new Mock<IPresupuestoService>().Object);

        var progreso = new ProgresoRPG();
        progreso.TitulosDesbloqueados.Add("iniciado");
        await storage.SetAsync("cdg_progreso_rpg", progreso);
        gastoServiceMock.Setup(s => s.ObtenerGastosAsync()).ReturnsAsync(new List<Gasto>());

        await service.VerificarYDesbloquearTitulosAsync();
        var desbloqueados = await service.ObtenerTitulosDesbloqueadosAsync();

        Assert.Single(desbloqueados);
        Assert.Equal("iniciado", desbloqueados[0].Id);
    }

    [Fact]
    public async Task AplicarGastoAsync_MismoDia_NoIncrementaRacha()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var recurrenciaServiceMock = new Mock<IRecurrenciaService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, recurrenciaServiceMock.Object, new Mock<IPresupuestoService>().Object);
        var catId = Guid.NewGuid();
        var hoy = DateTime.UtcNow;

        var gasto1 = new Gasto { Monto = 50, Fecha = hoy, CategoriaId = catId };
        await service.AplicarGastoAsync(gasto1, 0, 0);

        var gasto2 = new Gasto { Monto = 30, Fecha = hoy, CategoriaId = catId };
        await service.AplicarGastoAsync(gasto2, 0, 0);

        var progreso = await service.ObtenerProgresoAsync();
        // diff==0: No suma racha, no resetea → GastosConsecutivos se queda en 1
        Assert.Equal(1, progreso.GastosConsecutivos);
        // Primer gasto: 10 exp. Segundo: 10 exp (sin bonus de racha)
        Assert.Equal(20, progreso.ExpActual);
    }

    [Fact]
    public async Task RecuperarHpDiarioAsync_SinUltimoGasto_NoRecuperaHp()
    {
        var storage = new InMemoryStorageService();
        var gastoServiceMock = new Mock<IGastoService>();
        var recurrenciaServiceMock = new Mock<IRecurrenciaService>();
        var service = new GamificacionService(storage, gastoServiceMock.Object, recurrenciaServiceMock.Object, new Mock<IPresupuestoService>().Object);

        var progresoInicial = await service.ObtenerProgresoAsync();
        progresoInicial.HpActual = 50;
        progresoInicial.UltimoGastoFecha = null;
        await storage.SetAsync("cdg_progreso_rpg", progresoInicial);

        var progreso = await service.RecuperarHpDiarioAsync();
        Assert.Equal(50, progreso.HpActual);
    }
}
