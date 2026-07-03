using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class FinanciamientoService : IFinanciamientoService
{
    private const string StorageKey = "cdg_financiamientos";
    private const string BancosKey = "cdg_bancos_personalizados";

    private static readonly string[] BancosPredefinidos =
    [
        "Banorte", "BBVA", "Banamex", "HSBC", "Banregio",
        "Banco Azteca", "Nu Bank", "Plata Bank", "Didi Bank"
    ];

    private readonly IStorageService _storage;
    private readonly IUsuarioService _usuarioService;
    private readonly ISupabaseService _supabaseService;
    private readonly IGastoService _gastoService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<FinanciamientoService> _logger;

    public FinanciamientoService(
        IStorageService storage,
        IUsuarioService usuarioService,
        ISupabaseService supabaseService,
        IGastoService gastoService,
        IServiceProvider serviceProvider,
        ILogger<FinanciamientoService> logger)
    {
        _storage = storage;
        _usuarioService = usuarioService;
        _supabaseService = supabaseService;
        _gastoService = gastoService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<List<Financiamiento>> ObtenerFinanciamientosAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();

        if (usuario.PlanActivo == PlanType.Nube)
        {
            try
            {
                var uid = usuario.SupabaseUserId ?? usuario.Id.ToString();
                var filter = !string.IsNullOrEmpty(usuario.HogarId)
                    ? $"hogar_id=eq.{Uri.EscapeDataString(usuario.HogarId)}"
                    : $"usuario_id=eq.{Uri.EscapeDataString(uid)}";
                var remotos = await _supabaseService.ObtenerTodosAsync<Financiamiento>("financiamientos", filter);

                foreach (var r in remotos)
                    r.UsuarioId = usuario.Id;

                var locales = await _storage.GetAsync<List<Financiamiento>>(StorageKey) ?? new List<Financiamiento>();
                var idsRemotos = new HashSet<Guid>(remotos.Select(r => r.Id));

                var merged = new List<Financiamiento>(remotos);
                merged.AddRange(locales.Where(l => !idsRemotos.Contains(l.Id)));

                await _storage.SetAsync(StorageKey, merged);

                if (!string.IsNullOrEmpty(usuario.HogarId))
                    return merged.Where(i => i.HogarId == usuario.HogarId).ToList();
                return merged.Where(i => i.UsuarioId == usuario.Id).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener financiamientos desde la nube, usando local");
            }
        }

        var items = await _storage.GetAsync<List<Financiamiento>>(StorageKey);
        if (items == null) return new List<Financiamiento>();

        if (!string.IsNullOrEmpty(usuario.HogarId))
            return items.Where(i => i.HogarId == usuario.HogarId).ToList();
        return items.Where(i => i.UsuarioId == usuario.Id).ToList();
    }

    public async Task MigrarFinanciamientosAHogarAsync(string hogarId)
    {
        var items = await _storage.GetAsync<List<Financiamiento>>(StorageKey) ?? new List<Financiamiento>();
        foreach (var i in items)
        {
            if (string.IsNullOrEmpty(i.HogarId))
                i.HogarId = hogarId;
        }
        await _storage.SetAsync(StorageKey, items);
    }

    public async Task<Financiamiento> CrearFinanciamientoAsync(Financiamiento item)
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        item.UsuarioId = usuario.Id;
        item.HogarId = usuario.HogarId;
        item.CreadoEn = DateTime.UtcNow;
        item.Sincronizado = false;

        var items = await _storage.GetAsync<List<Financiamiento>>(StorageKey) ?? new List<Financiamiento>();
        items.Add(item);
        await _storage.SetAsync(StorageKey, items);

        if (usuario.PlanActivo == PlanType.Nube)
        {
            try
            {
                var uidOriginal = item.UsuarioId;
                if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                    item.UsuarioId = Guid.Parse(usuario.SupabaseUserId);
                await _supabaseService.GuardarAsync("financiamientos", item);
                item.UsuarioId = uidOriginal;
                item.Sincronizado = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al sincronizar financiamiento con la nube");
                item.Sincronizado = false;
            }

            var itemsActuales = await _storage.GetAsync<List<Financiamiento>>(StorageKey) ?? new List<Financiamiento>();
            var idx = itemsActuales.FindIndex(i => i.Id == item.Id);
            if (idx >= 0)
            {
                itemsActuales[idx] = item;
                await _storage.SetAsync(StorageKey, itemsActuales);
            }
        }

        await CrearGastoDesdeFinanciamientoAsync(item);

        item.ProximaCuota = item.FechaInicio.AddMonths(1);
        await ActualizarFinanciamientoAsync(item);

        return item;
    }

    public async Task<Financiamiento> ActualizarFinanciamientoAsync(Financiamiento item)
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        item.ActualizadoEn = DateTime.UtcNow;

        if (usuario.PlanActivo == PlanType.Nube)
        {
            try
            {
                var uidOriginal = item.UsuarioId;
                if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                    item.UsuarioId = Guid.Parse(usuario.SupabaseUserId);
                await _supabaseService.ActualizarAsync("financiamientos", item.Id, item);
                item.UsuarioId = uidOriginal;
                item.Sincronizado = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al sincronizar financiamiento con la nube");
                item.Sincronizado = false;
            }
        }
        else
        {
            item.Sincronizado = false;
        }

        var items = await _storage.GetAsync<List<Financiamiento>>(StorageKey) ?? new List<Financiamiento>();
        var index = items.FindIndex(i => i.Id == item.Id);
        if (index >= 0)
        {
            items[index] = item;
            await _storage.SetAsync(StorageKey, items);
        }

        return item;
    }

    public async Task EliminarFinanciamientoAsync(Guid id)
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        var esNube = usuario.PlanActivo == PlanType.Nube;

        if (esNube)
        {
            var sync = _serviceProvider.GetRequiredService<ISyncService>();
            await sync.RegistrarPendienteEliminarAsync("financiamientos", id);
        }

        var items = await _storage.GetAsync<List<Financiamiento>>(StorageKey) ?? new List<Financiamiento>();
        items.RemoveAll(i => i.Id == id);
        await _storage.SetAsync(StorageKey, items);

        if (esNube)
        {
            try { await _supabaseService.EliminarAsync<Financiamiento>("financiamientos", id); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al sincronizar eliminación de financiamiento con la nube");
            }
        }
    }

    public async Task EliminarFinanciamientoConGastosAsync(Guid id)
    {
        var gastos = await _gastoService.ObtenerGastosAsync();
        var gastosAEliminar = gastos.Where(g => g.FinanciamientoId == id).ToList();

        foreach (var gasto in gastosAEliminar)
        {
            await _gastoService.EliminarGastoAsync(gasto.Id);
        }

        await EliminarFinanciamientoAsync(id);
    }

    public async Task<List<Gasto>> GenerarCuotasPendientesAsync()
    {
        var generados = new List<Gasto>();
        var items = await ObtenerFinanciamientosAsync();

        var pendientes = items.Where(f =>
            f.Activo &&
            f.ProximaCuota != default &&
            f.ProximaCuota <= DateTime.UtcNow &&
            f.ProximaCuota <= f.FechaInicio.AddMonths(f.PlazoMeses))
            .ToList();

        if (!pendientes.Any()) return generados;

        foreach (var f in pendientes)
        {
            while (f.ProximaCuota <= DateTime.UtcNow && f.ProximaCuota <= f.FechaInicio.AddMonths(f.PlazoMeses))
            {
                var montoPago = CalcularPagoAmortizado(f.MontoTotal, f.PlazoMeses, f.TasaInteresAnual);
                var gastosExistentes = (await _gastoService.ObtenerGastosAsync())
                    .Count(g => g.FinanciamientoId == f.Id);
                var cuotaNum = gastosExistentes + 1;

                var gasto = new Gasto
                {
                    CategoriaId = f.CategoriaId ?? Guid.Empty,
                    Monto = montoPago,
                    Descripcion = $"{f.Tipo} - {f.Alias} (cuota {cuotaNum}/{f.PlazoMeses})",
                    Fecha = f.ProximaCuota,
                    HogarId = f.HogarId,
                    FinanciamientoId = f.Id,
                };

                await _gastoService.CrearGastoAsync(gasto);
                generados.Add(gasto);

                f.ProximaCuota = f.ProximaCuota.AddMonths(1);
            }

            var fechaFin = f.FechaInicio.AddMonths(f.PlazoMeses);
            if (f.ProximaCuota > fechaFin)
                f.Activo = false;

            await ActualizarFinanciamientoAsync(f);
        }

        return generados;
    }

    public async Task<List<string>> ObtenerBancosAsync()
    {
        var personalizados = await _storage.GetAsync<List<string>>(BancosKey) ?? new List<string>();
        return BancosPredefinidos
            .Concat(personalizados)
            .Distinct()
            .OrderBy(b => b)
            .ToList();
    }

    public async Task AgregarBancoPersonalizadoAsync(string banco)
    {
        if (string.IsNullOrWhiteSpace(banco)) return;
        var normalizado = banco.Trim();
        if (BancosPredefinidos.Contains(normalizado, StringComparer.OrdinalIgnoreCase)) return;

        var personalizados = await _storage.GetAsync<List<string>>(BancosKey) ?? new List<string>();
        if (personalizados.Any(b => b.Equals(normalizado, StringComparison.OrdinalIgnoreCase))) return;

        personalizados.Add(normalizado);
        await _storage.SetAsync(BancosKey, personalizados);
    }

    public static decimal CalcularPagoAmortizado(decimal montoTotal, int plazoMeses, decimal? tasaInteresAnual)
    {
        if (plazoMeses <= 0) return montoTotal;

        if (tasaInteresAnual.HasValue && tasaInteresAnual > 0 && montoTotal > 0)
        {
            var r = (tasaInteresAnual.Value / 100m) / 12m;
            var unoMasR = 1m + r;
            var potencia = (decimal)Math.Pow((double)unoMasR, plazoMeses);
            if (potencia != 1)
                return montoTotal * r * potencia / (potencia - 1m);
        }

        return montoTotal / plazoMeses;
    }

    private async Task CrearGastoDesdeFinanciamientoAsync(Financiamiento item)
    {
        try
        {
            var montoPago = CalcularPagoAmortizado(item.MontoTotal, item.PlazoMeses, item.TasaInteresAnual);

            var fechaPago = DateTime.UtcNow;

            var gastosExistentes = (await _gastoService.ObtenerGastosAsync())
                .Count(g => g.FinanciamientoId == item.Id);
            var cuotaNum = gastosExistentes + 1;
            var descripcion = $"{item.Tipo} - {item.Alias} (cuota {cuotaNum}/{item.PlazoMeses})";

            var gasto = new Gasto
            {
                CategoriaId = item.CategoriaId ?? Guid.Empty,
                Monto = montoPago,
                Descripcion = descripcion,
                Fecha = fechaPago,
                HogarId = item.HogarId,
                FinanciamientoId = item.Id,
            };

            await _gastoService.CrearGastoAsync(gasto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear gasto desde financiamiento");
        }
    }
}
