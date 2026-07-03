using Microsoft.Extensions.Logging;
using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class PresupuestoService : IPresupuestoService
{
    private const string StorageKey = "cdg_presupuestos";
    private readonly IStorageService _storage;
    private readonly IGastoService _gastoService;
    private readonly IUsuarioService _usuarioService;
    private readonly ISupabaseService _supabaseService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PresupuestoService> _logger;

    public PresupuestoService(
        IStorageService storage,
        IGastoService gastoService,
        IUsuarioService usuarioService,
        ISupabaseService supabaseService,
        IServiceProvider serviceProvider,
        ILogger<PresupuestoService> logger)
    {
        _storage = storage;
        _gastoService = gastoService;
        _usuarioService = usuarioService;
        _supabaseService = supabaseService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<List<Presupuesto>> ObtenerPresupuestosAsync()
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
                var remotos = await _supabaseService.ObtenerTodosAsync<Presupuesto>("presupuestos", filter);

                foreach (var r in remotos)
                    r.UsuarioId = usuario.Id;

                var locales = await _storage.GetAsync<List<Presupuesto>>(StorageKey) ?? new List<Presupuesto>();
                var idsRemotos = new HashSet<Guid>(remotos.Select(r => r.Id));

                var merged = new List<Presupuesto>(remotos);
                merged.AddRange(locales.Where(l => !idsRemotos.Contains(l.Id)));

                await _storage.SetAsync(StorageKey, merged);

                if (!string.IsNullOrEmpty(usuario.HogarId))
                    return merged.Where(p => p.HogarId == usuario.HogarId).ToList();
                return merged.Where(p => p.UsuarioId == usuario.Id).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener presupuestos desde la nube, usando local");
            }
        }

        var presupuestos = await _storage.GetAsync<List<Presupuesto>>(StorageKey);
        if (presupuestos == null) return new List<Presupuesto>();

        if (!string.IsNullOrEmpty(usuario.HogarId))
            return presupuestos.Where(p => p.HogarId == usuario.HogarId).ToList();
        return presupuestos.Where(p => p.UsuarioId == usuario.Id).ToList();
    }

    public async Task MigrarPresupuestosAHogarAsync(string hogarId)
    {
        var presupuestos = await _storage.GetAsync<List<Presupuesto>>(StorageKey) ?? new List<Presupuesto>();
        foreach (var p in presupuestos)
        {
            if (string.IsNullOrEmpty(p.HogarId))
                p.HogarId = hogarId;
        }
        await _storage.SetAsync(StorageKey, presupuestos);
    }

    public async Task<Presupuesto> CrearPresupuestoAsync(Presupuesto presupuesto)
    {
        var presupuestos = await _storage.GetAsync<List<Presupuesto>>(StorageKey) ?? new List<Presupuesto>();
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        presupuesto.HogarId = usuario.HogarId;
        presupuesto.UsuarioId = usuario.Id;
        presupuesto.ActualizadoEn = DateTime.UtcNow;
        presupuestos.Add(presupuesto);
        await _storage.SetAsync(StorageKey, presupuestos);

        if (usuario.PlanActivo == PlanType.Nube)
        {
            try
            {
                var uidOriginal = presupuesto.UsuarioId;
                if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                    presupuesto.UsuarioId = Guid.Parse(usuario.SupabaseUserId);
                await _supabaseService.GuardarAsync("presupuestos", presupuesto);
                presupuesto.UsuarioId = uidOriginal;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al sincronizar presupuesto con la nube"); }
        }

        return presupuesto;
    }

    public async Task EliminarPresupuestoAsync(Guid id)
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        var esNube = usuario.PlanActivo == PlanType.Nube;

        if (esNube)
        {
            var sync = _serviceProvider.GetRequiredService<ISyncService>();
            await sync.RegistrarPendienteEliminarAsync("presupuestos", id);
        }

        var presupuestos = await _storage.GetAsync<List<Presupuesto>>(StorageKey) ?? new List<Presupuesto>();
        presupuestos.RemoveAll(p => p.Id == id);
        await _storage.SetAsync(StorageKey, presupuestos);

        if (esNube)
        {
            try { await _supabaseService.EliminarAsync<Presupuesto>("presupuestos", id); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al sincronizar presupuesto con la nube");
            }
        }
    }

    public async Task ActualizarPresupuestoAsync(Presupuesto presupuesto)
    {
        var presupuestos = await _storage.GetAsync<List<Presupuesto>>(StorageKey) ?? new List<Presupuesto>();
        var index = presupuestos.FindIndex(p => p.Id == presupuesto.Id);
        if (index < 0) return;

        presupuesto.ActualizadoEn = DateTime.UtcNow;
        presupuestos[index] = presupuesto;
        await _storage.SetAsync(StorageKey, presupuestos);

        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario.PlanActivo == PlanType.Nube)
        {
            try
            {
                var uidOriginal = presupuesto.UsuarioId;
                if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                    presupuesto.UsuarioId = Guid.Parse(usuario.SupabaseUserId);
                await _supabaseService.ActualizarAsync("presupuestos", presupuesto.Id, presupuesto);
                presupuesto.UsuarioId = uidOriginal;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al sincronizar presupuesto con la nube"); }
        }
    }

    public async Task<decimal> ObtenerGastadoEnPeriodoAsync(Presupuesto presupuesto)
    {
        var gastos = await _gastoService.ObtenerGastosAsync();
        gastos = await FiltrarGastosParaPresupuestoAsync(gastos);
        return await CalcularGastadoAsync(presupuesto, gastos);
    }

    public async Task<List<Gasto>> FiltrarGastosParaPresupuestoAsync(List<Gasto> gastos)
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario.ExcluirRecurrentesDePresupuesto)
            gastos = gastos.Where(g => g.RecurrenciaId == null).ToList();
        if (usuario.ExcluirCreditosDePresupuesto)
            gastos = gastos.Where(g => g.FinanciamientoId == null).ToList();
        return gastos;
    }

    public Task<decimal> CalcularGastadoAsync(Presupuesto presupuesto, List<Gasto> gastos)
    {
        var inicio = presupuesto.FechaInicio.Kind == DateTimeKind.Utc
            ? presupuesto.FechaInicio.ToLocalTime()
            : presupuesto.FechaInicio;
        var fin = presupuesto.FechaFin.HasValue
            ? (presupuesto.FechaFin.Value.Kind == DateTimeKind.Utc
                ? presupuesto.FechaFin.Value.ToLocalTime()
                : presupuesto.FechaFin.Value)
            : presupuesto.Periodo switch
            {
                PeriodoPresupuesto.Semanal => presupuesto.FechaInicio.AddDays(7),
                PeriodoPresupuesto.Mensual => presupuesto.FechaInicio.AddMonths(1),
                PeriodoPresupuesto.Anual => presupuesto.FechaInicio.AddYears(1),
                _ => DateTime.MaxValue
            };

        var gastosEnPeriodo = gastos.Where(g =>
        {
            var fechaLocal = g.Fecha.Kind == DateTimeKind.Utc ? g.Fecha.ToLocalTime() : g.Fecha;
            return fechaLocal >= inicio && fechaLocal <= fin
                && (presupuesto.CategoriaId == null || g.CategoriaId == presupuesto.CategoriaId);
        });

        return Task.FromResult(gastosEnPeriodo.Where(g => g.Monto > 0).Sum(g => g.Monto));
    }
}
