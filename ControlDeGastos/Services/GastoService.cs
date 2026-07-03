using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class GastoService : IGastoService
{
    private const string StorageKey = "cdg_gastos";
    private readonly IStorageService _storage;
    private readonly IUsuarioService _usuarioService;
    private readonly ILicenciaService _licenciaService;
    private readonly ISupabaseService _supabaseService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<GastoService> _logger;

    public GastoService(
        IStorageService storage,
        IUsuarioService usuarioService,
        ILicenciaService licenciaService,
        ISupabaseService supabaseService,
        IServiceProvider serviceProvider,
        ILogger<GastoService> logger)
    {
        _storage = storage;
        _usuarioService = usuarioService;
        _licenciaService = licenciaService;
        _supabaseService = supabaseService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    private static DateTime ObtenerFechaGastoLocal(Gasto gasto)
    {
        return gasto.Fecha.Kind == DateTimeKind.Utc
            ? gasto.Fecha.ToLocalTime()
            : gasto.Fecha;
    }

    public async Task<List<Gasto>> ObtenerGastosAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();

        if (usuario.PlanActivo == PlanType.Nube)
        {
            try
            {
                await SincronizarGastosDesdeNubeAsync(usuario);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener gastos desde la nube, usando local");
            }
        }

        var gastos = await _storage.GetAsync<List<Gasto>>(StorageKey);
        if (gastos == null) return new List<Gasto>();

        if (!string.IsNullOrEmpty(usuario.HogarId))
            return gastos.Where(g => g.HogarId == usuario.HogarId).ToList();
        return gastos.Where(g => g.UsuarioId == usuario.Id).ToList();
    }

    private async Task SincronizarGastosDesdeNubeAsync(Usuario usuario)
    {
        var userId = !string.IsNullOrEmpty(usuario.SupabaseUserId)
            ? usuario.SupabaseUserId
            : usuario.Id.ToString();
        var filter = !string.IsNullOrEmpty(usuario.HogarId)
            ? $"hogar_id=eq.{Uri.EscapeDataString(usuario.HogarId)}"
            : $"usuario_id=eq.{Uri.EscapeDataString(userId)}";
        var remotos = await _supabaseService.ObtenerTodosAsync<Gasto>("gastos", filter);

        foreach (var r in remotos)
        {
            r.UsuarioId = usuario.Id;
            r.Sincronizado = true;
        }

        var locales = await _storage.GetAsync<List<Gasto>>(StorageKey) ?? new List<Gasto>();
        var noSincronizados = locales.Where(g => !g.Sincronizado).ToList();
        var idsRemotos = new HashSet<Guid>(remotos.Select(r => r.Id));

        var merged = new List<Gasto>(remotos);
        merged.AddRange(noSincronizados.Where(n => !idsRemotos.Contains(n.Id)));

        await _storage.SetAsync(StorageKey, merged);
    }

    public async Task<List<Gasto>> ObtenerGastosPorMesAsync(int year, int month)
    {
        var gastos = await ObtenerGastosAsync();
        return gastos.Where(g => ObtenerFechaGastoLocal(g).Year == year && ObtenerFechaGastoLocal(g).Month == month)
                     .OrderByDescending(g => g.Fecha)
                     .ToList();
    }

    public async Task<List<Gasto>> ObtenerGastosPorRangoAsync(DateTime desde, DateTime hasta)
    {
        var gastos = await ObtenerGastosAsync();
        return gastos.Where(g => ObtenerFechaGastoLocal(g).Date >= desde.Date && ObtenerFechaGastoLocal(g).Date <= hasta.Date)
                     .OrderByDescending(g => g.Fecha)
                     .ToList();
    }

    public async Task<PaginatedResult<Gasto>> ObtenerGastosPaginadoAsync(Paginacion paginacion, FiltroGasto? filtro = null)
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();

        if (usuario.PlanActivo == PlanType.Nube)
        {
            try
            {
                await SincronizarGastosDesdeNubeAsync(usuario);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener gastos paginados desde la nube, usando local");
            }
        }

        return await ObtenerGastosPaginadoLocalAsync(paginacion, filtro, usuario);
    }

    private async Task<PaginatedResult<Gasto>> ObtenerGastosPaginadoLocalAsync(Paginacion pag, FiltroGasto? filtro, Usuario usuario)
    {
        var gastos = await _storage.GetAsync<List<Gasto>>(StorageKey);
        if (gastos == null)
            return new PaginatedResult<Gasto> { Pagina = pag.Pagina, TamanoPagina = pag.TamanoPagina };

        var query = gastos.AsEnumerable();

        if (!string.IsNullOrEmpty(usuario.HogarId))
            query = query.Where(g => g.HogarId == usuario.HogarId);
        else
            query = query.Where(g => g.UsuarioId == usuario.Id);

        if (filtro != null)
        {
            if (filtro.Desde.HasValue)
                query = query.Where(g => g.Fecha >= filtro.Desde.Value);
            if (filtro.Hasta.HasValue)
                query = query.Where(g => g.Fecha <= filtro.Hasta.Value);
            if (filtro.Year.HasValue)
                query = query.Where(g => ObtenerFechaGastoLocal(g).Year == filtro.Year.Value);
            if (filtro.Month.HasValue)
                query = query.Where(g => ObtenerFechaGastoLocal(g).Month == filtro.Month.Value);
            if (filtro.CategoriaId.HasValue)
                query = query.Where(g => g.CategoriaId == filtro.CategoriaId.Value);
            if (!string.IsNullOrEmpty(filtro.TextoBusqueda))
                query = query.Where(g => g.Descripcion?.Contains(filtro.TextoBusqueda, StringComparison.OrdinalIgnoreCase) == true);
        }

        var lista = query.OrderByDescending(g => g.Fecha).ToList();
        var total = lista.Count;
        var paged = lista.Skip(pag.Skip).Take(pag.TamanoPagina).ToList();

        return new PaginatedResult<Gasto>
        {
            Items = paged,
            Total = total,
            Pagina = pag.Pagina,
            TamanoPagina = pag.TamanoPagina
        };
    }

    public async Task<Gasto> CrearGastoAsync(Gasto gasto)
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        gasto.UsuarioId = usuario.Id;
        gasto.HogarId = usuario.HogarId;
        gasto.CreadoEn = DateTime.UtcNow;
        gasto.Sincronizado = false;

        var gastos = await _storage.GetAsync<List<Gasto>>(StorageKey) ?? new List<Gasto>();
        gastos.Add(gasto);
        await _storage.SetAsync(StorageKey, gastos);

        if (usuario.ModoGamificadoActivo)
        {
            var presupuestoService = _serviceProvider.GetRequiredService<IPresupuestoService>();
            var presupuestos = await presupuestoService.ObtenerPresupuestosAsync();
            var presupuesto = presupuestos.FirstOrDefault(p => p.CategoriaId == gasto.CategoriaId);
            var gastado = presupuesto != null
                ? await presupuestoService.ObtenerGastadoEnPeriodoAsync(presupuesto)
                : 0m;
            var limite = presupuesto?.MontoLimite ?? 0m;

            var gamificacionService = _serviceProvider.GetRequiredService<IGamificacionService>();
            await gamificacionService.AplicarGastoAsync(gasto, gastado, limite);
        }

        if (usuario.PlanActivo == PlanType.Nube)
        {
            try
            {
                var usuarioIdOriginal = gasto.UsuarioId;
                if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                    gasto.UsuarioId = Guid.Parse(usuario.SupabaseUserId);

                var creado = await _supabaseService.GuardarAsync("gastos", gasto);

                gasto.UsuarioId = usuarioIdOriginal;
                if (creado is not null)
                    gasto.UpdatedAt = creado.UpdatedAt;
                gasto.Sincronizado = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al sincronizar gasto con la nube");
                gasto.Sincronizado = false;
            }

            var gastosActuales = await _storage.GetAsync<List<Gasto>>(StorageKey) ?? new List<Gasto>();
            var idx = gastosActuales.FindIndex(g => g.Id == gasto.Id);
            if (idx >= 0)
            {
                gastosActuales[idx] = gasto;
                await _storage.SetAsync(StorageKey, gastosActuales);
            }
        }

        return gasto;
    }

    public async Task<Gasto> ActualizarGastoAsync(Gasto gasto)
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        var timestampOriginal = gasto.UpdatedAt ?? gasto.ActualizadoEn;
        gasto.ActualizadoEn = DateTime.UtcNow;

        if (usuario.PlanActivo == PlanType.Nube)
        {
            try
            {
                var remotos = await _supabaseService.ObtenerTodosAsync<Gasto>("gastos", $"id=eq.{gasto.Id}");
                if (remotos.Count > 0)
                {
                    var remoto = remotos[0];
                    var tiempoRemoto = remoto.UpdatedAt ?? remoto.ActualizadoEn ?? remoto.CreadoEn;
                    var tiempoLocal = timestampOriginal ?? gasto.CreadoEn;
                    if (tiempoRemoto > tiempoLocal)
                    {
                        throw new InvalidOperationException("⚠️ Alguien más modificó este gasto mientras lo editabas. Recarga e inténtalo de nuevo.");
                    }
                }
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                _logger.LogWarning(ex, "Error al verificar conflicto de gasto en la nube");
            }

            try
            {
                var usuarioIdOriginal = gasto.UsuarioId;
                if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                    gasto.UsuarioId = Guid.Parse(usuario.SupabaseUserId);

                var actualizado = await _supabaseService.ActualizarAsync("gastos", gasto.Id, gasto);

                gasto.UsuarioId = usuarioIdOriginal;
                if (actualizado is not null)
                    gasto.UpdatedAt = actualizado.UpdatedAt;
                gasto.Sincronizado = true;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al sincronizar gasto con la nube"); gasto.Sincronizado = false; }
        }
        else
        {
            gasto.Sincronizado = false;
        }

        var gastos = await _storage.GetAsync<List<Gasto>>(StorageKey) ?? new List<Gasto>();
        var index = gastos.FindIndex(g => g.Id == gasto.Id);
        if (index >= 0)
        {
            gastos[index] = gasto;
            await _storage.SetAsync(StorageKey, gastos);
        }

        return gasto;
    }

    public async Task EliminarGastoAsync(Guid id)
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        var esNube = usuario.PlanActivo == PlanType.Nube;

        if (esNube)
        {
            var sync = _serviceProvider.GetRequiredService<ISyncService>();
            await sync.RegistrarPendienteEliminarAsync("gastos", id);
        }

        var gastos = await _storage.GetAsync<List<Gasto>>(StorageKey) ?? new List<Gasto>();
        gastos.RemoveAll(g => g.Id == id);
        await _storage.SetAsync(StorageKey, gastos);

        if (esNube)
        {
            try { await _supabaseService.EliminarAsync<Gasto>("gastos", id); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al sincronizar eliminación de gasto con la nube");
            }
        }
    }

    public async Task MarcarTodosPendientesSyncAsync()
    {
        var gastos = await _storage.GetAsync<List<Gasto>>(StorageKey) ?? new List<Gasto>();
        foreach (var g in gastos)
            g.Sincronizado = false;
        await _storage.SetAsync(StorageKey, gastos);
    }

    public async Task MigrarGastosAHogarAsync(string hogarId)
    {
        var gastos = await _storage.GetAsync<List<Gasto>>(StorageKey) ?? new List<Gasto>();
        foreach (var g in gastos)
        {
            if (string.IsNullOrEmpty(g.HogarId))
            {
                g.HogarId = hogarId;
                g.Sincronizado = false;
            }
        }
        await _storage.SetAsync(StorageKey, gastos);
    }
}
