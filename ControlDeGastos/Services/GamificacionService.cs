using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class GamificacionService : IGamificacionService
{
    private const string StorageKey = "cdg_progreso_rpg";
    private readonly IStorageService _storage;
    private readonly IGastoService _gastoService;
    private readonly IRecurrenciaService _recurrenciaService;
    private readonly IPresupuestoService _presupuestoService;
    private static readonly SemaphoreSlim ProgresoLock = new(1, 1);

    private static readonly List<Logro> LogrosPredefinidos = new List<Logro>
    {
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000001"), Nombre = "Primer paso", Descripcion = "Registra tu primer gasto", Icono = "🎯", TipoCondicion = TipoCondicionLogro.GastosTotales, ValorCondicion = 1, RecompensaExp = 20, RecompensaMonedas = 100, Orden = 1 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000002"), Nombre = "Aprendiz", Descripcion = "Registra 5 gastos", Icono = "📝", TipoCondicion = TipoCondicionLogro.GastosTotales, ValorCondicion = 5, RecompensaExp = 30, RecompensaMonedas = 150, Orden = 2 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000003"), Nombre = "Constante", Descripcion = "Registra 10 gastos", Icono = "📋", TipoCondicion = TipoCondicionLogro.GastosTotales, ValorCondicion = 10, RecompensaExp = 50, RecompensaMonedas = 200, Orden = 3 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000004"), Nombre = "Dedicado", Descripcion = "Registra 25 gastos", Icono = "📊", TipoCondicion = TipoCondicionLogro.GastosTotales, ValorCondicion = 25, RecompensaExp = 80, RecompensaMonedas = 300, Orden = 4 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000005"), Nombre = "Experto", Descripcion = "Registra 50 gastos", Icono = "💪", TipoCondicion = TipoCondicionLogro.GastosTotales, ValorCondicion = 50, RecompensaExp = 120, RecompensaMonedas = 500, Orden = 5 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000006"), Nombre = "Legendario", Descripcion = "Registra 100 gastos", Icono = "🏆", TipoCondicion = TipoCondicionLogro.GastosTotales, ValorCondicion = 100, RecompensaExp = 200, RecompensaMonedas = 800, Orden = 6 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000007"), Nombre = "Racha inicial", Descripcion = "3 días consecutivos registrando gastos", Icono = "🔥", TipoCondicion = TipoCondicionLogro.GastosConsecutivos, ValorCondicion = 3, RecompensaExp = 25, RecompensaMonedas = 100, Orden = 7 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000008"), Nombre = "Racha semanal", Descripcion = "7 días consecutivos registrando gastos", Icono = "🔥", TipoCondicion = TipoCondicionLogro.GastosConsecutivos, ValorCondicion = 7, RecompensaExp = 60, RecompensaMonedas = 200, Orden = 8 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000009"), Nombre = "Racha mensual", Descripcion = "30 días consecutivos registrando gastos", Icono = "🔥", TipoCondicion = TipoCondicionLogro.GastosConsecutivos, ValorCondicion = 30, RecompensaExp = 150, RecompensaMonedas = 500, Orden = 9 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-00000000000a"), Nombre = "Novato", Descripcion = "Alcanza el nivel 3", Icono = "⭐", TipoCondicion = TipoCondicionLogro.NivelAlcanzado, ValorCondicion = 3, RecompensaExp = 0, RecompensaMonedas = 200, Orden = 10 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-00000000000b"), Nombre = "Experimentado", Descripcion = "Alcanza el nivel 10", Icono = "⭐", TipoCondicion = TipoCondicionLogro.NivelAlcanzado, ValorCondicion = 10, RecompensaExp = 0, RecompensaMonedas = 500, Orden = 11 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-00000000000c"), Nombre = "Maestro", Descripcion = "Alcanza el nivel 25", Icono = "⭐", TipoCondicion = TipoCondicionLogro.NivelAlcanzado, ValorCondicion = 25, RecompensaExp = 0, RecompensaMonedas = 1000, Orden = 12 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-00000000000d"), Nombre = "Primer ingreso", Descripcion = "Registra tu primer ingreso", Icono = "💰", TipoCondicion = TipoCondicionLogro.IngresosRegistrados, ValorCondicion = 1, RecompensaExp = 20, RecompensaMonedas = 100, Orden = 13 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-00000000000e"), Nombre = "Variedad", Descripcion = "Usa 5 categorías distintas", Icono = "🎨", TipoCondicion = TipoCondicionLogro.CategoriasUsadas, ValorCondicion = 5, RecompensaExp = 40, RecompensaMonedas = 200, Orden = 14 },

        new() { Id = Guid.Parse("10010000-0000-0000-0000-00000000000f"), Nombre = "Gastador inicial", Descripcion = "Acumula $10,000 en gastos totales", Icono = "🪙", TipoCondicion = TipoCondicionLogro.MontoTotalGastado, ValorCondicion = 10000, RecompensaExp = 30, RecompensaMonedas = 300, Orden = 15 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000010"), Nombre = "Gastador pro", Descripcion = "Acumula $50,000 en gastos totales", Icono = "💰", TipoCondicion = TipoCondicionLogro.MontoTotalGastado, ValorCondicion = 50000, RecompensaExp = 80, RecompensaMonedas = 500, Orden = 16 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000011"), Nombre = "Gastador leyenda", Descripcion = "Acumula $200,000 en gastos totales", Icono = "💎", TipoCondicion = TipoCondicionLogro.MontoTotalGastado, ValorCondicion = 200000, RecompensaExp = 200, RecompensaMonedas = 1000, Orden = 17 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000012"), Nombre = "Ingresos estables", Descripcion = "Registra 5 ingresos", Icono = "📈", TipoCondicion = TipoCondicionLogro.IngresosRegistrados, ValorCondicion = 5, RecompensaExp = 30, RecompensaMonedas = 200, Orden = 18 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000013"), Nombre = "Multi-ingresos", Descripcion = "Registra 20 ingresos", Icono = "📊", TipoCondicion = TipoCondicionLogro.IngresosRegistrados, ValorCondicion = 20, RecompensaExp = 80, RecompensaMonedas = 400, Orden = 19 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000014"), Nombre = "Fuente de ingresos", Descripcion = "Registra 50 ingresos", Icono = "🏦", TipoCondicion = TipoCondicionLogro.IngresosRegistrados, ValorCondicion = 50, RecompensaExp = 150, RecompensaMonedas = 800, Orden = 20 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000015"), Nombre = "Variedad II", Descripcion = "Usa 10 categorías distintas", Icono = "🎨", TipoCondicion = TipoCondicionLogro.CategoriasUsadas, ValorCondicion = 10, RecompensaExp = 80, RecompensaMonedas = 400, Orden = 21 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000016"), Nombre = "Variedad III", Descripcion = "Usa 15 categorías distintas", Icono = "🌈", TipoCondicion = TipoCondicionLogro.CategoriasUsadas, ValorCondicion = 15, RecompensaExp = 150, RecompensaMonedas = 600, Orden = 22 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000017"), Nombre = "Racha quincenal", Descripcion = "14 días consecutivos registrando gastos", Icono = "🔥", TipoCondicion = TipoCondicionLogro.GastosConsecutivos, ValorCondicion = 14, RecompensaExp = 100, RecompensaMonedas = 300, Orden = 23 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000018"), Nombre = "Racha bimestral", Descripcion = "60 días consecutivos registrando gastos", Icono = "🔥", TipoCondicion = TipoCondicionLogro.GastosConsecutivos, ValorCondicion = 60, RecompensaExp = 250, RecompensaMonedas = 800, Orden = 24 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000019"), Nombre = "Racha centenario", Descripcion = "100 días consecutivos registrando gastos", Icono = "🔥", TipoCondicion = TipoCondicionLogro.GastosConsecutivos, ValorCondicion = 100, RecompensaExp = 500, RecompensaMonedas = 1000, Orden = 25 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-00000000001a"), Nombre = "Nivel 5", Descripcion = "Alcanza el nivel 5", Icono = "⭐", TipoCondicion = TipoCondicionLogro.NivelAlcanzado, ValorCondicion = 5, RecompensaExp = 0, RecompensaMonedas = 300, Orden = 26 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-00000000001b"), Nombre = "Nivel 15", Descripcion = "Alcanza el nivel 15", Icono = "⭐", TipoCondicion = TipoCondicionLogro.NivelAlcanzado, ValorCondicion = 15, RecompensaExp = 0, RecompensaMonedas = 800, Orden = 27 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-00000000001c"), Nombre = "Nivel 20", Descripcion = "Alcanza el nivel 20", Icono = "⭐", TipoCondicion = TipoCondicionLogro.NivelAlcanzado, ValorCondicion = 20, RecompensaExp = 0, RecompensaMonedas = 800, Orden = 28 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-00000000001d"), Nombre = "Gastos controlados", Descripcion = "Registra 250 gastos", Icono = "💪", TipoCondicion = TipoCondicionLogro.GastosTotales, ValorCondicion = 250, RecompensaExp = 300, RecompensaMonedas = 1000, Orden = 29 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-00000000001e"), Nombre = "Maestro del gasto", Descripcion = "Registra 500 gastos", Icono = "🏆", TipoCondicion = TipoCondicionLogro.GastosTotales, ValorCondicion = 500, RecompensaExp = 500, RecompensaMonedas = 1500, Orden = 30 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-00000000001f"), Nombre = "Leyenda del gasto", Descripcion = "Registra 1000 gastos", Icono = "👑", TipoCondicion = TipoCondicionLogro.GastosTotales, ValorCondicion = 1000, RecompensaExp = 1000, RecompensaMonedas = 2000, Orden = 31 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000020"), Nombre = "Social", Descripcion = "Registra tu primer gasto compartido", Icono = "👥", TipoCondicion = TipoCondicionLogro.GastosCompartidos, ValorCondicion = 1, RecompensaExp = 20, RecompensaMonedas = 100, Orden = 32 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000021"), Nombre = "Hogareño", Descripcion = "Registra 10 gastos compartidos", Icono = "🏠", TipoCondicion = TipoCondicionLogro.GastosCompartidos, ValorCondicion = 10, RecompensaExp = 80, RecompensaMonedas = 300, Orden = 33 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000022"), Nombre = "Comunitario", Descripcion = "Registra 50 gastos compartidos", Icono = "🌍", TipoCondicion = TipoCondicionLogro.GastosCompartidos, ValorCondicion = 50, RecompensaExp = 200, RecompensaMonedas = 800, Orden = 34 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000023"), Nombre = "Planificador", Descripcion = "Crea tu primer gasto recurrente", Icono = "📅", TipoCondicion = TipoCondicionLogro.RecurrenciasActivas, ValorCondicion = 1, RecompensaExp = 20, RecompensaMonedas = 100, Orden = 35 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000024"), Nombre = "Estratega", Descripcion = "Mantén 5 gastos recurrentes activos", Icono = "📅", TipoCondicion = TipoCondicionLogro.RecurrenciasActivas, ValorCondicion = 5, RecompensaExp = 60, RecompensaMonedas = 300, Orden = 36 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000025"), Nombre = "Automatizado", Descripcion = "Mantén 15 gastos recurrentes activos", Icono = "🤖", TipoCondicion = TipoCondicionLogro.RecurrenciasActivas, ValorCondicion = 15, RecompensaExp = 150, RecompensaMonedas = 500, Orden = 37 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000026"), Nombre = "Presupuesto inicial", Descripcion = "Crea tu primer presupuesto", Icono = "📋", TipoCondicion = TipoCondicionLogro.PresupuestosCreados, ValorCondicion = 1, RecompensaExp = 20, RecompensaMonedas = 100, Orden = 38 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000027"), Nombre = "Planificador mensual", Descripcion = "Crea 5 presupuestos", Icono = "📆", TipoCondicion = TipoCondicionLogro.PresupuestosCreados, ValorCondicion = 5, RecompensaExp = 80, RecompensaMonedas = 300, Orden = 39 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000028"), Nombre = "Presupuesto cumplido", Descripcion = "Completa 1 mes sin exceder tu presupuesto", Icono = "✅", TipoCondicion = TipoCondicionLogro.PresupuestosCumplidos, ValorCondicion = 1, RecompensaExp = 30, RecompensaMonedas = 200, Orden = 40 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-000000000029"), Nombre = "Ahorrador consistente", Descripcion = "Completa 3 meses sin exceder tu presupuesto", Icono = "🏅", TipoCondicion = TipoCondicionLogro.PresupuestosCumplidos, ValorCondicion = 3, RecompensaExp = 100, RecompensaMonedas = 500, Orden = 41 },
        new() { Id = Guid.Parse("10010000-0000-0000-0000-00000000002a"), Nombre = "Maestro del ahorro", Descripcion = "Completa 6 meses sin exceder tu presupuesto", Icono = "👑", TipoCondicion = TipoCondicionLogro.PresupuestosCumplidos, ValorCondicion = 6, RecompensaExp = 300, RecompensaMonedas = 1000, Orden = 42 },
    };

    private static readonly List<TituloCosmetico> TitulosPredefinidos = new List<TituloCosmetico>
    {
        new() { Id = "iniciado", Nombre = "Iniciado", Descripcion = "Desbloquea el logro \"Primer paso\"", Icono = "🎖️", TipoCondicion = TipoCondicionTitulo.LogroEspecifico, LogroRequerido = Guid.Parse("10010000-0000-0000-0000-000000000001"), Orden = 1 },
        new() { Id = "racha_7", Nombre = "Racha imparable", Descripcion = "Mantén 7 días de racha consecutiva", Icono = "🔥", TipoCondicion = TipoCondicionTitulo.RachaMinima, ValorCondicion = 7, Orden = 2 },
        new() { Id = "racha_30", Nombre = "Racha legendaria", Descripcion = "Mantén 30 días de racha consecutiva", Icono = "💫", TipoCondicion = TipoCondicionTitulo.RachaMinima, ValorCondicion = 30, Orden = 3 },
        new() { Id = "nivel_10", Nombre = "Veterano", Descripcion = "Alcanza el nivel 10", Icono = "⭐", TipoCondicion = TipoCondicionTitulo.NivelMinimo, ValorCondicion = 10, Orden = 4 },
        new() { Id = "nivel_25", Nombre = "Leyenda viviente", Descripcion = "Alcanza el nivel 25", Icono = "👑", TipoCondicion = TipoCondicionTitulo.NivelMinimo, ValorCondicion = 25, Orden = 5 },
        new() { Id = "ahorrador_100k", Nombre = "Ahorrador profesional", Descripcion = "Acumula $100,000 de ahorro neto", Icono = "💰", TipoCondicion = TipoCondicionTitulo.MontoAhorrado, ValorCondicion = 100000, Orden = 6 },
        new() { Id = "completista", Nombre = "Completista", Descripcion = "Desbloquea 25 logros", Icono = "🏆", TipoCondicion = TipoCondicionTitulo.LogrosTotales, ValorCondicion = 25, Orden = 7 },
        new() { Id = "compartido_10", Nombre = "Maestro del hogar", Descripcion = "Registra 10 gastos compartidos", Icono = "🏠", TipoCondicion = TipoCondicionTitulo.GastosCompartidos, ValorCondicion = 10, Orden = 8 },
    };

    public GamificacionService(IStorageService storage, IGastoService gastoService, IRecurrenciaService recurrenciaService, IPresupuestoService presupuestoService)
    {
        _storage = storage;
        _gastoService = gastoService;
        _recurrenciaService = recurrenciaService;
        _presupuestoService = presupuestoService;
    }

    public async Task<ProgresoRPG> ObtenerProgresoAsync()
    {
        var progreso = await _storage.GetAsync<ProgresoRPG>(StorageKey);
        return progreso ?? new ProgresoRPG();
    }

    public Task<List<Logro>> ObtenerLogrosAsync()
    {
        return Task.FromResult(LogrosPredefinidos);
    }

    public async Task<List<Logro>> ObtenerLogrosDesbloqueadosAsync()
    {
        var progreso = await ObtenerProgresoAsync();
        return LogrosPredefinidos
            .Where(l => progreso.LogrosDesbloqueados.Contains(l.Id))
            .ToList();
    }

    private static void AplicarLevelUp(ProgresoRPG progreso)
    {
        while (progreso.ExpActual >= progreso.ExpRequerida)
        {
            progreso.ExpActual -= progreso.ExpRequerida;
            progreso.Nivel++;
            progreso.ExpRequerida += 50;
            progreso.HpMaximo += 10;
            progreso.HpActual = Math.Min(progreso.HpActual + 20, progreso.HpMaximo);
        }
    }

    private static DateTime ObtenerFechaGastoLocal(Gasto gasto)
    {
        return gasto.Fecha.Kind == DateTimeKind.Utc
            ? gasto.Fecha.ToLocalTime()
            : gasto.Fecha;
    }

    public async Task AplicarGastoAsync(Gasto gasto, decimal gastadoPeriodo, decimal limitePeriodo)
    {
        await ProgresoLock.WaitAsync();
        try
        {
            var progreso = await ObtenerProgresoAsync();

            var fechaGasto = ObtenerFechaGastoLocal(gasto);
            if (progreso.UltimoResetGastosMes != fechaGasto.Month || progreso.UltimoResetGastosAnio != fechaGasto.Year)
            {
                if (progreso.UltimoResetGastosMes != 0 && !progreso.PresupuestoExcedidoEsteMes)
                    progreso.MesesPresupuestoRespetado++;
                progreso.GastosEstePeriodo = 0;
                progreso.PresupuestoExcedidoEsteMes = false;
                progreso.UltimoResetGastosMes = fechaGasto.Month;
                progreso.UltimoResetGastosAnio = fechaGasto.Year;
            }

            progreso.GastosEstePeriodo++;

            progreso.IdsCategoriasUsadas.Add(gasto.CategoriaId.ToString());

            var estaSobrePresupuesto = limitePeriodo > 0 && gastadoPeriodo > limitePeriodo;

            if (estaSobrePresupuesto)
                progreso.PresupuestoExcedidoEsteMes = true;

            if (estaSobrePresupuesto && gasto.Monto > 0)
            {
                progreso.HpActual = Math.Max(0, progreso.HpActual - 5);
            }

            if (gasto.Monto > 0)
            {
                var baseExp = 10;
                var exp = baseExp;

                if (progreso.UltimoGastoFecha.HasValue)
                {
                    var diff = (fechaGasto.Date - progreso.UltimoGastoFecha.Value.Date).Days;
                    if (diff == 1)
                    {
                        exp += 5;
                        progreso.GastosConsecutivos++;
                    }
                    else if (diff == 0)
                    {
                        // mismo día: no suma racha, no resetea
                    }
                    else
                    {
                        if (progreso.EscudosRacha > 0)
                        {
                            progreso.EscudosRacha--;
                        }
                        else
                        {
                            progreso.GastosConsecutivos = 1;
                        }
                    }
                }
                else
                {
                    progreso.GastosConsecutivos = 1;
                }

                if (progreso.GastosConsecutivos >= 3)
                    exp += 5;

                if (progreso.BoostExpExpiracion.HasValue && progreso.BoostExpExpiracion.Value > DateTime.UtcNow && progreso.BoostExpMultiplicador > 1.0)
                    exp = (int)(exp * progreso.BoostExpMultiplicador);
                else if (progreso.BoostExpExpiracion.HasValue && progreso.BoostExpExpiracion.Value <= DateTime.UtcNow)
                {
                    progreso.BoostExpMultiplicador = 1.0;
                    progreso.BoostExpExpiracion = null;
                }

                progreso.ExpActual += exp;

                var monedas = 50;
                if (progreso.GastosConsecutivos >= 3) monedas += 10;
                progreso.Monedas += monedas;

                while (progreso.ExpActual >= progreso.ExpRequerida)
                {
                    progreso.ExpActual -= progreso.ExpRequerida;
                    progreso.Nivel++;
                    progreso.ExpRequerida += 50;
                    progreso.HpMaximo += 10;
                    progreso.HpActual = Math.Min(progreso.HpActual + 20, progreso.HpMaximo);
                }

                progreso.UltimoGastoFecha = fechaGasto;
            }
            else if (gasto.Monto < 0)
            {
                progreso.Monedas += 75;
            }

            await _storage.SetAsync(StorageKey, progreso);
        }
        finally { ProgresoLock.Release(); }
    }

    public async Task<(double multiplicador, DateTime? expiracion)> ObtenerBoostExpActivoAsync()
    {
        var progreso = await ObtenerProgresoAsync();

        if (progreso.BoostExpExpiracion.HasValue && progreso.BoostExpExpiracion.Value < DateTime.UtcNow)
        {
            progreso.BoostExpMultiplicador = 1.0;
            progreso.BoostExpExpiracion = null;
            await _storage.SetAsync(StorageKey, progreso);
            return (1.0, null);
        }

        return (progreso.BoostExpMultiplicador, progreso.BoostExpExpiracion);
    }

    public async Task<ProgresoRPG> RecuperarHpDiarioAsync()
    {
        await ProgresoLock.WaitAsync();
        try
        {
            var progreso = await ObtenerProgresoAsync();

            var hoyLocal = DateTime.Now.Date;
            if (progreso.UltimoGastoFecha.HasValue &&
                progreso.UltimoGastoFecha.Value.Date < hoyLocal)
            {
                progreso.HpActual = Math.Min(progreso.HpActual + 10, progreso.HpMaximo);
            }

            await _storage.SetAsync(StorageKey, progreso);
            return progreso;
        }
        finally { ProgresoLock.Release(); }
    }

    public async Task<(int actual, int requerido)> CalcularProgresoLogroAsync(Logro logro)
    {
        var progreso = await ObtenerProgresoAsync();
        var gastos = await _gastoService.ObtenerGastosAsync();
        var recurrenciasActivas = logro.TipoCondicion == TipoCondicionLogro.RecurrenciasActivas
            ? (await _recurrenciaService.ObtenerRecurrenciasAsync()).Count(r => r.Activa)
            : 0;

        return logro.TipoCondicion switch
        {
            TipoCondicionLogro.GastosTotales => (gastos.Count(g => g.Monto > 0), logro.ValorCondicion),
            TipoCondicionLogro.GastosConsecutivos => (progreso.GastosConsecutivos, logro.ValorCondicion),
            TipoCondicionLogro.NivelAlcanzado => (progreso.Nivel, logro.ValorCondicion),
            TipoCondicionLogro.IngresosRegistrados => (gastos.Count(g => g.Monto < 0), logro.ValorCondicion),
            TipoCondicionLogro.CategoriasUsadas => (progreso.IdsCategoriasUsadas.Count, logro.ValorCondicion),
            TipoCondicionLogro.MontoTotalGastado => ((int)gastos.Where(g => g.Monto > 0).Sum(g => g.Monto), logro.ValorCondicion),
            TipoCondicionLogro.GastosCompartidos => (gastos.Count(g => g.EsGastoCompartido), logro.ValorCondicion),
            TipoCondicionLogro.RecurrenciasActivas => (recurrenciasActivas, logro.ValorCondicion),
            TipoCondicionLogro.PresupuestosCreados => ((await _presupuestoService.ObtenerPresupuestosAsync()).Count, logro.ValorCondicion),
            TipoCondicionLogro.PresupuestosCumplidos => (progreso.MesesPresupuestoRespetado, logro.ValorCondicion),
            _ => (0, logro.ValorCondicion)
        };
    }

    public async Task RecalcularDesdeCeroAsync()
    {
        var gastos = await _gastoService.ObtenerGastosAsync();
        var gastosOrdenados = gastos.OrderBy(g => g.Fecha).ToList();

        await ProgresoLock.WaitAsync();
        try
        {
            var actual = await ObtenerProgresoAsync();
            var hpGuardado = actual.HpActual;

            var nuevo = new ProgresoRPG();

            foreach (var gasto in gastosOrdenados)
            {
                var gastoLocal = ObtenerFechaGastoLocal(gasto);

                if (nuevo.UltimoResetGastosMes != gastoLocal.Month || nuevo.UltimoResetGastosAnio != gastoLocal.Year)
                {
                    nuevo.GastosEstePeriodo = 0;
                    nuevo.UltimoResetGastosMes = gastoLocal.Month;
                    nuevo.UltimoResetGastosAnio = gastoLocal.Year;
                }
                nuevo.GastosEstePeriodo++;

                nuevo.IdsCategoriasUsadas.Add(gasto.CategoriaId.ToString());

                if (gasto.Monto > 0)
                {
                    var exp = 10;

                    if (nuevo.UltimoGastoFecha.HasValue)
                    {
                        var diff = (gastoLocal.Date - nuevo.UltimoGastoFecha.Value.Date).Days;
                        if (diff == 1)
                        {
                            exp += 5;
                            nuevo.GastosConsecutivos++;
                        }
                        else if (diff == 0)
                        {
                            // mismo día
                        }
                        else
                        {
                            nuevo.GastosConsecutivos = 1;
                        }
                    }
                    else
                    {
                        nuevo.GastosConsecutivos = 1;
                    }

                    if (nuevo.GastosConsecutivos >= 3)
                        exp += 5;

                    if (actual.BoostExpExpiracion.HasValue && actual.BoostExpExpiracion.Value > DateTime.UtcNow && actual.BoostExpMultiplicador > 1.0)
                        exp = (int)(exp * actual.BoostExpMultiplicador);

                    nuevo.ExpActual += exp;

                    var monedas = 50;
                    if (nuevo.GastosConsecutivos >= 3) monedas += 10;
                    nuevo.Monedas += monedas;

                    AplicarLevelUp(nuevo);

                    nuevo.UltimoGastoFecha = gastoLocal;
                }
            }

            nuevo.HpActual = hpGuardado > nuevo.HpMaximo ? nuevo.HpMaximo : hpGuardado;
            nuevo.MonedasGastadas = actual.MonedasGastadas;
            nuevo.BoostExpMultiplicador = actual.BoostExpMultiplicador;
            nuevo.BoostExpExpiracion = actual.BoostExpExpiracion;
            nuevo.BoostExpItemId = actual.BoostExpItemId;
            nuevo.HpEscudoRestante = actual.HpEscudoRestante;
            nuevo.PresupuestoExcedidoEsteMes = actual.PresupuestoExcedidoEsteMes;
            nuevo.MesesPresupuestoRespetado = actual.MesesPresupuestoRespetado;
            nuevo.UltimoMesVerificadoPresupuesto = actual.UltimoMesVerificadoPresupuesto;
            nuevo.UltimoAnioVerificadoPresupuesto = actual.UltimoAnioVerificadoPresupuesto;
            nuevo.SkinTarjetaActiva = actual.SkinTarjetaActiva;
            nuevo.IdsSkinsCompradas = actual.IdsSkinsCompradas.ToList();
            nuevo.IdsTitulosTienda = actual.IdsTitulosTienda.ToList();

            await _storage.SetAsync(StorageKey, nuevo);

            await VerificarYDesbloquearInternalAsync();

            var final = await ObtenerProgresoAsync();
            final.Monedas = Math.Max(0, final.Monedas - final.MonedasGastadas);
            await _storage.SetAsync(StorageKey, final);
        }
        finally { ProgresoLock.Release(); }
    }

    public async Task<List<Logro>> VerificarYDesbloquearLogrosAsync()
    {
        await ProgresoLock.WaitAsync();
        try
        {
            return await VerificarYDesbloquearInternalAsync();
        }
        finally { ProgresoLock.Release(); }
    }

    private async Task<List<Logro>> VerificarYDesbloquearInternalAsync()
    {
        var progreso = await ObtenerProgresoAsync();
        var gastos = await _gastoService.ObtenerGastosAsync();
        var recurrencias = await _recurrenciaService.ObtenerRecurrenciasAsync();
        var recurrenciasActivas = recurrencias.Count(r => r.Activa);
        var desbloqueadosAhora = new List<Logro>();

        foreach (var logro in LogrosPredefinidos)
        {
            if (progreso.LogrosDesbloqueados.Contains(logro.Id))
                continue;

            var cumple = logro.TipoCondicion switch
            {
                TipoCondicionLogro.GastosTotales => gastos.Count(g => g.Monto > 0) >= logro.ValorCondicion,
                TipoCondicionLogro.MontoTotalGastado => gastos.Where(g => g.Monto > 0).Sum(g => g.Monto) >= logro.ValorCondicion,
                TipoCondicionLogro.GastosConsecutivos => progreso.GastosConsecutivos >= logro.ValorCondicion,
                TipoCondicionLogro.NivelAlcanzado => progreso.Nivel >= logro.ValorCondicion,
                TipoCondicionLogro.IngresosRegistrados => gastos.Count(g => g.Monto < 0) >= logro.ValorCondicion,
                TipoCondicionLogro.CategoriasUsadas => progreso.IdsCategoriasUsadas.Count >= logro.ValorCondicion,
                TipoCondicionLogro.GastosCompartidos => gastos.Count(g => g.EsGastoCompartido) >= logro.ValorCondicion,
                TipoCondicionLogro.RecurrenciasActivas => recurrenciasActivas >= logro.ValorCondicion,
                TipoCondicionLogro.PresupuestosCreados => (await _presupuestoService.ObtenerPresupuestosAsync()).Count >= logro.ValorCondicion,
                TipoCondicionLogro.PresupuestosCumplidos => progreso.MesesPresupuestoRespetado >= logro.ValorCondicion,
                _ => false
            };

            if (cumple)
            {
                progreso.LogrosDesbloqueados.Add(logro.Id);

                if (logro.RecompensaExp > 0)
                {
                    progreso.ExpActual += logro.RecompensaExp;
                    AplicarLevelUp(progreso);
                }

                if (logro.RecompensaMonedas > 0)
                    progreso.Monedas += logro.RecompensaMonedas;

                desbloqueadosAhora.Add(logro);
            }
        }

        if (desbloqueadosAhora.Count > 0)
            await _storage.SetAsync(StorageKey, progreso);

        await VerificarYDesbloquearTitulosInternalAsync(gastos);

        return desbloqueadosAhora;
    }

    public Task<List<TituloCosmetico>> ObtenerTitulosAsync()
    {
        return Task.FromResult(TitulosPredefinidos);
    }

    public async Task<List<TituloCosmetico>> ObtenerTitulosDesbloqueadosAsync()
    {
        var progreso = await ObtenerProgresoAsync();
        return TitulosPredefinidos
            .Where(t => progreso.TitulosDesbloqueados.Contains(t.Id))
            .ToList();
    }

    public async Task<string?> ObtenerTituloActivoNombreAsync()
    {
        var progreso = await ObtenerProgresoAsync();
        if (string.IsNullOrEmpty(progreso.TituloActivoId))
            return null;
        var titulo = TitulosPredefinidos.FirstOrDefault(t => t.Id == progreso.TituloActivoId);
        return titulo?.Nombre;
    }

    public async Task<bool> EstablecerTituloActivoAsync(string? tituloId)
    {
        await ProgresoLock.WaitAsync();
        try
        {
            var progreso = await ObtenerProgresoAsync();

            if (tituloId is not null && !progreso.TitulosDesbloqueados.Contains(tituloId))
                return false;

            progreso.TituloActivoId = tituloId;
            await _storage.SetAsync(StorageKey, progreso);
            return true;
        }
        finally { ProgresoLock.Release(); }
    }

    public async Task VerificarYDesbloquearTitulosAsync()
    {
        await ProgresoLock.WaitAsync();
        try
        {
            await VerificarYDesbloquearTitulosInternalAsync();
        }
        finally { ProgresoLock.Release(); }
    }

    private async Task VerificarYDesbloquearTitulosInternalAsync(List<Gasto>? gastosCache = null)
    {
        var progreso = await ObtenerProgresoAsync();
        var gastos = gastosCache ?? await _gastoService.ObtenerGastosAsync();
        var desbloqueoNuevo = false;

        foreach (var titulo in TitulosPredefinidos)
        {
            if (progreso.TitulosDesbloqueados.Contains(titulo.Id))
                continue;

            var cumple = titulo.TipoCondicion switch
            {
                TipoCondicionTitulo.LogroEspecifico when titulo.LogroRequerido.HasValue
                    => progreso.LogrosDesbloqueados.Contains(titulo.LogroRequerido.Value),
                TipoCondicionTitulo.RachaMinima
                    => progreso.GastosConsecutivos >= titulo.ValorCondicion,
                TipoCondicionTitulo.NivelMinimo
                    => progreso.Nivel >= titulo.ValorCondicion,
                TipoCondicionTitulo.LogrosTotales
                    => progreso.LogrosDesbloqueados.Count >= titulo.ValorCondicion,
                TipoCondicionTitulo.MontoAhorrado
                    => CalcularAhorroNeto(gastos) >= titulo.ValorCondicion,
                TipoCondicionTitulo.GastosCompartidos
                    => gastos.Count(g => g.EsGastoCompartido) >= titulo.ValorCondicion,
                _ => false
            };

            if (cumple)
            {
                progreso.TitulosDesbloqueados.Add(titulo.Id);
                desbloqueoNuevo = true;
            }
        }

        if (desbloqueoNuevo)
            await _storage.SetAsync(StorageKey, progreso);
    }

    private static decimal CalcularAhorroNeto(List<Gasto> gastos)
    {
        var ingresos = gastos.Where(g => g.Monto < 0).Sum(g => Math.Abs(g.Monto));
        var gastosTotales = gastos.Where(g => g.Monto > 0).Sum(g => g.Monto);
        return ingresos - gastosTotales;
    }
}
