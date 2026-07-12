using Microsoft.Extensions.Logging;
using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class RecurrenciaService : IRecurrenciaService
{
    private const string StorageKey = "cdg_recurrencias";
    private readonly IStorageService _storage;
    private readonly IUsuarioService _usuarioService;
    private readonly IGastoService _gastoService;
    private readonly ISupabaseService _supabaseService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecurrenciaService> _logger;

    public RecurrenciaService(
        IStorageService storage,
        IUsuarioService usuarioService,
        IGastoService gastoService,
        ISupabaseService supabaseService,
        IServiceProvider serviceProvider,
        ILogger<RecurrenciaService> logger)
    {
        _storage = storage;
        _usuarioService = usuarioService;
        _gastoService = gastoService;
        _supabaseService = supabaseService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<List<Recurrencia>> ObtenerRecurrenciasAsync()
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
                var remotos = await _supabaseService.ObtenerTodosAsync<Recurrencia>("recurrencias", filter);

                foreach (var r in remotos)
                    r.UsuarioId = usuario.Id;

                var locales = await _storage.GetAsync<List<Recurrencia>>(StorageKey) ?? new List<Recurrencia>();
                var idsRemotos = new HashSet<Guid>(remotos.Select(r => r.Id));

                var merged = new List<Recurrencia>(remotos);
                merged.AddRange(locales.Where(l => !idsRemotos.Contains(l.Id)));

                await _storage.SetAsync(StorageKey, merged);

                if (!string.IsNullOrEmpty(usuario.HogarId))
                    return merged.Where(r => r.HogarId == usuario.HogarId).ToList();
                return merged.Where(r => r.UsuarioId == usuario.Id).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener recurrencias desde la nube, usando local");
            }
        }

        var recurrencias = await _storage.GetAsync<List<Recurrencia>>(StorageKey);
        if (recurrencias == null) return new List<Recurrencia>();

        if (!string.IsNullOrEmpty(usuario.HogarId))
            return recurrencias.Where(r => r.HogarId == usuario.HogarId).ToList();
        return recurrencias.Where(r => r.UsuarioId == usuario.Id).ToList();
    }

    public async Task MigrarRecurrenciasAHogarAsync(string hogarId)
    {
        var recurrencias = await _storage.GetAsync<List<Recurrencia>>(StorageKey) ?? new List<Recurrencia>();
        foreach (var r in recurrencias)
        {
            if (string.IsNullOrEmpty(r.HogarId))
                r.HogarId = hogarId;
        }
        await _storage.SetAsync(StorageKey, recurrencias);
    }

    public async Task<Recurrencia> CrearRecurrenciaAsync(Recurrencia recurrencia)
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        recurrencia.UsuarioId = usuario.Id;
        recurrencia.HogarId = usuario.HogarId;
        recurrencia.ProximaFecha = CalcularSiguienteFecha(recurrencia.FechaInicio, recurrencia.TipoRecurrencia, recurrencia.Intervalo);
        recurrencia.CreadoEn = DateTime.UtcNow;
        recurrencia.ActualizadoEn = DateTime.UtcNow;
        recurrencia.Sincronizado = false;

        var recurrencias = await _storage.GetAsync<List<Recurrencia>>(StorageKey) ?? new List<Recurrencia>();
        recurrencias.Add(recurrencia);
        await _storage.SetAsync(StorageKey, recurrencias);

        if (recurrencia.FechaInicio <= DateTime.UtcNow)
        {
            var gasto = new Gasto
            {
                UsuarioId = recurrencia.UsuarioId,
                CategoriaId = recurrencia.CategoriaId ?? Guid.Empty,
                Monto = recurrencia.Monto,
                Descripcion = recurrencia.Descripcion,
                Fecha = recurrencia.FechaInicio,
                HogarId = recurrencia.HogarId,
                RecurrenciaId = recurrencia.Id,
            };
            await _gastoService.CrearGastoAsync(gasto);
        }

        if (usuario.PlanActivo == PlanType.Nube)
        {
            try
            {
                var uidOriginal = recurrencia.UsuarioId;
                if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                    recurrencia.UsuarioId = Guid.Parse(usuario.SupabaseUserId);
                await _supabaseService.GuardarAsync("recurrencias", recurrencia);
                recurrencia.UsuarioId = uidOriginal;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al sincronizar recurrencia con la nube"); }
        }

        return recurrencia;
    }

    private static DateTime CalcularSiguienteFecha(DateTime desde, TipoRecurrencia tipo, int intervalo)
    {
        return tipo switch
        {
            TipoRecurrencia.Diario => desde.AddDays(intervalo),
            TipoRecurrencia.Semanal => desde.AddDays(7 * intervalo),
            TipoRecurrencia.Mensual => desde.AddMonths(intervalo),
            TipoRecurrencia.Anual => desde.AddYears(intervalo),
            _ => desde.AddMonths(1),
        };
    }

    public async Task ActualizarRecurrenciaAsync(Recurrencia recurrencia)
    {
        recurrencia.ActualizadoEn = DateTime.UtcNow;

        var recurrencias = await _storage.GetAsync<List<Recurrencia>>(StorageKey) ?? new List<Recurrencia>();
        var index = recurrencias.FindIndex(r => r.Id == recurrencia.Id);
        if (index >= 0)
        {
            var oldCategoriaId = recurrencias[index].CategoriaId;
            var oldMonto = recurrencias[index].Monto;
            recurrencias[index] = recurrencia;
            await _storage.SetAsync(StorageKey, recurrencias);

            if (oldCategoriaId != recurrencia.CategoriaId || oldMonto != recurrencia.Monto)
            {
                var gastos = await _gastoService.ObtenerGastosAsync();
                foreach (var g in gastos.Where(g => g.RecurrenciaId == recurrencia.Id))
                {
                    g.Monto = recurrencia.Monto;
                    g.CategoriaId = recurrencia.CategoriaId ?? Guid.Empty;
                    await _gastoService.ActualizarGastoAsync(g);
                }
            }
        }

        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario.PlanActivo == PlanType.Nube)
        {
            try
            {
                var uidOriginal = recurrencia.UsuarioId;
                if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                    recurrencia.UsuarioId = Guid.Parse(usuario.SupabaseUserId);
                await _supabaseService.ActualizarAsync("recurrencias", recurrencia.Id, recurrencia);
                recurrencia.UsuarioId = uidOriginal;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al sincronizar recurrencia con la nube"); }
        }
    }

    public async Task EliminarRecurrenciaAsync(Guid id)
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        var esNube = usuario.PlanActivo == PlanType.Nube;

        if (esNube)
        {
            var sync = _serviceProvider.GetRequiredService<ISyncService>();
            await sync.RegistrarPendienteEliminarAsync("recurrencias", id);
        }

        var recurrencias = await _storage.GetAsync<List<Recurrencia>>(StorageKey) ?? new List<Recurrencia>();
        recurrencias.RemoveAll(r => r.Id == id);
        await _storage.SetAsync(StorageKey, recurrencias);

        if (esNube)
        {
            try { await _supabaseService.EliminarAsync<Recurrencia>("recurrencias", id); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al sincronizar recurrencia con la nube");
            }
        }
    }

    public async Task EliminarRecurrenciaConGastosAsync(Guid id)
    {
        var gastos = await _gastoService.ObtenerGastosAsync();
        var gastosAEliminar = gastos.Where(g => g.RecurrenciaId == id).ToList();

        foreach (var gasto in gastosAEliminar)
        {
            await _gastoService.EliminarGastoAsync(gasto.Id);
        }

        await EliminarRecurrenciaAsync(id);
    }

    public async Task<List<Gasto>> GenerarPendientesAsync()
    {
        var generados = new List<Gasto>();
        var recurrencias = await ObtenerRecurrenciasAsync();
        var pendientes = recurrencias
            .Where(r => r.Activa && r.ProximaFecha <= DateTime.UtcNow && (r.FechaFin == null || r.FechaFin > DateTime.UtcNow))
            .ToList();

        if (!pendientes.Any()) return generados;

        var todosGastos = await _storage.GetAsync<List<Gasto>>("cdg_gastos") ?? new List<Gasto>();

        foreach (var rec in pendientes)
        {
            var gasto = new Gasto
            {
                Id = Guid.NewGuid(),
                UsuarioId = rec.UsuarioId,
                CategoriaId = rec.CategoriaId ?? Guid.Empty,
                Monto = rec.Monto,
                Descripcion = rec.Descripcion,
                Fecha = rec.ProximaFecha,
                HogarId = rec.HogarId,
                RecurrenciaId = rec.Id,
                CreadoEn = DateTime.UtcNow,
                Sincronizado = false,
            };

            todosGastos.Add(gasto);
            generados.Add(gasto);

            rec.ProximaFecha = rec.TipoRecurrencia switch
            {
                TipoRecurrencia.Diario => rec.ProximaFecha.AddDays(rec.Intervalo),
                TipoRecurrencia.Semanal => rec.ProximaFecha.AddDays(7 * rec.Intervalo),
                TipoRecurrencia.Mensual => rec.ProximaFecha.AddMonths(rec.Intervalo),
                TipoRecurrencia.Anual => rec.ProximaFecha.AddYears(rec.Intervalo),
                _ => rec.ProximaFecha.AddMonths(1),
            };
            rec.ActualizadoEn = DateTime.UtcNow;
        }

        // Guardar recurrencias actualizadas primero (evita duplicados si crash)
        var todas = await _storage.GetAsync<List<Recurrencia>>(StorageKey) ?? new List<Recurrencia>();
        foreach (var rec in pendientes)
        {
            var idx = todas.FindIndex(r => r.Id == rec.Id);
            if (idx >= 0) todas[idx] = rec;
        }
        await _storage.SetAsync(StorageKey, todas);

        // Guardar gastos generados
        await _storage.SetAsync("cdg_gastos", todosGastos);

        return generados;
    }
}
