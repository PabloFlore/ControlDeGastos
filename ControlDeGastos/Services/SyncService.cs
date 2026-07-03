using Microsoft.Extensions.Logging;
using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class SyncService : ISyncService
{
    private const string SyncStateKey = "cdg_sync_state";

    private readonly IStorageService _storage;
    private readonly ISupabaseService _supabase;
    private readonly IUsuarioService _usuarioService;
    private readonly IGastoService _gastoService;
    private readonly ICategoriaService _categoriaService;
    private readonly IPresupuestoService _presupuestoService;
    private readonly IRecurrenciaService _recurrenciaService;
    private readonly IFinanciamientoService _financiamientoService;
    private static readonly SemaphoreSlim SyncLock = new(1, 1);
    private bool _huboErrorSync;
    private readonly ILogger<SyncService> _logger;

    public SyncService(
        IStorageService storage,
        ISupabaseService supabase,
        IUsuarioService usuarioService,
        IGastoService gastoService,
        ICategoriaService categoriaService,
        IPresupuestoService presupuestoService,
        IRecurrenciaService recurrenciaService,
        IFinanciamientoService financiamientoService,
        IConnectivityService connectivity,
        ILogger<SyncService> logger)
    {
        _storage = storage;
        _supabase = supabase;
        _usuarioService = usuarioService;
        _gastoService = gastoService;
        _categoriaService = categoriaService;
        _presupuestoService = presupuestoService;
        _recurrenciaService = recurrenciaService;
        _financiamientoService = financiamientoService;
        _connectivity = connectivity;
        _logger = logger;

        _connectivity.ConnectivityChanged += OnConnectivityChanged;
    }

    private readonly IConnectivityService _connectivity;

    private async void OnConnectivityChanged(bool online)
    {
        if (!online) return;

        var estado = await ObtenerEstadoSyncAsync();
        if (estado.HaySyncPendiente)
        {
            _logger.LogInformation("Conexión restaurada. Ejecutando sync pendiente...");
            estado.HaySyncPendiente = false;
            await _storage.SetAsync(SyncStateKey, estado);
            await SincronizarAhoraAsync();
        }
    }

    public async Task<SyncState> ObtenerEstadoSyncAsync()
    {
        var estado = await _storage.GetAsync<SyncState>(SyncStateKey);
        if (estado == null)
        {
            estado = new SyncState();
            await _storage.SetAsync(SyncStateKey, estado);
        }

        if (estado.Sincronizando && estado.SincronizandoDesde.HasValue &&
            (DateTime.UtcNow - estado.SincronizandoDesde.Value).TotalMinutes >= 2)
        {
            estado.Sincronizando = false;
            estado.SincronizandoDesde = null;
            estado.MensajeError = "La sincronización anterior no se completó (se recuperó automáticamente).";
            await _storage.SetAsync(SyncStateKey, estado);
        }

        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario?.PlanActivo == PlanType.Nube)
        {
            var gastos = await _gastoService.ObtenerGastosAsync();
            estado.PendientesSubir = gastos.Count(g => !g.Sincronizado) + (usuario.Sincronizado ? 0 : 1);
        }
        else
        {
            estado.PendientesSubir = 0;
            estado.PendientesBajar = 0;
        }

        return estado;
    }

    public async Task SincronizarAhoraAsync()
    {
        if (!await SyncLock.WaitAsync(0))
        {
            var estadoOcupado = await ObtenerEstadoSyncAsync();
            estadoOcupado.SyncSaltada = true;
            estadoOcupado.SaltadaRazon = "Ya hay una sincronización en curso";
            await _storage.SetAsync(SyncStateKey, estadoOcupado);
            return;
        }
        try
        {
            if (!_connectivity.IsOnline)
            {
                var estadoOffline = await ObtenerEstadoSyncAsync();
                estadoOffline.HaySyncPendiente = true;
                estadoOffline.SyncSaltada = true;
                estadoOffline.SaltadaRazon = "Sin conexión a internet";
                estadoOffline.MensajeError = "Sin conexión. La sincronización se realizará cuando haya conexión.";
                await _storage.SetAsync(SyncStateKey, estadoOffline);
                _logger.LogInformation("Sin conexión. Sync encolado para cuando haya internet.");
                return;
            }

            var usuario = await _usuarioService.ObtenerUsuarioAsync();
            if (usuario?.PlanActivo != PlanType.Nube)
            {
                var estadoNoNube = await ObtenerEstadoSyncAsync();
                estadoNoNube.SyncSaltada = true;
                estadoNoNube.SaltadaRazon = "Plan no es Nube";
                await _storage.SetAsync(SyncStateKey, estadoNoNube);
                return;
            }

            var estado = await ObtenerEstadoSyncAsync();
            estado.Sincronizando = true;
            estado.SincronizandoDesde = DateTime.UtcNow;
            estado.SyncSaltada = false;
            estado.SaltadaRazon = null;
            await _storage.SetAsync(SyncStateKey, estado);

            _huboErrorSync = false;
            var checkpoint = estado.UltimoCheckpoint;

            if (!CheckpointAlcanzado(checkpoint, "pendientes_eliminar"))
            {
                await ProcesarPendientesEliminarAsync();
                await GuardarCheckpointAsync("pendientes_eliminar");
            }

            if (!CheckpointAlcanzado(checkpoint, "gastos"))
            {
                await PushGastosAsync();
                await PullGastosAsync();
                await GuardarCheckpointAsync("gastos");
            }

            if (!CheckpointAlcanzado(checkpoint, "categorias"))
            {
                await PushCategoriasAsync();
                await PullCategoriasAsync();
                await GuardarCheckpointAsync("categorias");
            }

            if (!CheckpointAlcanzado(checkpoint, "presupuestos"))
            {
                await PushPresupuestosAsync();
                await PullPresupuestosAsync();
                await GuardarCheckpointAsync("presupuestos");
            }

            if (!CheckpointAlcanzado(checkpoint, "recurrencias"))
            {
                await PushRecurrenciasAsync();
                await PullRecurrenciasAsync();
                await GuardarCheckpointAsync("recurrencias");
            }

            if (!CheckpointAlcanzado(checkpoint, "financiamientos"))
            {
                await PushFinanciamientosAsync();
                await PullFinanciamientosAsync();
                await GuardarCheckpointAsync("financiamientos");
            }

            if (!CheckpointAlcanzado(checkpoint, "perfiles"))
            {
                await PushPerfilesAsync();
                await PullPerfilesAsync();
                await GuardarCheckpointAsync("perfiles");
            }

            estado.Sincronizando = false;
            estado.SincronizandoDesde = null;
            estado.UltimoCheckpoint = null;
            estado.SyncSaltada = false;
            estado.SaltadaRazon = null;
            if (!_huboErrorSync)
            {
                estado.UltimaSync = DateTime.UtcNow;
                estado.MensajeError = null;
            }
            else
            {
                estado.MensajeError = "Algunos datos no se pudieron sincronizar. Revisa tu conexión.";
            }
            await _storage.SetAsync(SyncStateKey, estado);

            if (_huboErrorSync)
                throw new InvalidOperationException("Algunos datos no se pudieron sincronizar. Revisa tu conexión.");
        }
        finally
        {
            SyncLock.Release();
        }
    }

    private static readonly string[] CheckpointOrder = ["pendientes_eliminar", "gastos", "categorias", "presupuestos", "recurrencias", "financiamientos", "suscripciones"];

    private static bool CheckpointAlcanzado(string? checkpoint, string target)
    {
        if (string.IsNullOrEmpty(checkpoint)) return false;
        var cpIdx = Array.IndexOf(CheckpointOrder, checkpoint);
        var tgtIdx = Array.IndexOf(CheckpointOrder, target);
        return cpIdx >= tgtIdx;
    }

    private async Task GuardarCheckpointAsync(string checkpoint)
    {
        var estado = await ObtenerEstadoSyncAsync();
        estado.UltimoCheckpoint = checkpoint;
        await _storage.SetAsync(SyncStateKey, estado);
    }

    public async Task RegistrarPendienteEliminarAsync(string tabla, Guid id)
    {
        var estado = await ObtenerEstadoSyncAsync();
        estado.PendientesEliminar.RemoveAll(p => p.Tabla == tabla && p.Id == id);
        estado.PendientesEliminar.Add(new PendienteEliminarSync
        {
            Tabla = tabla,
            Id = id,
            Fecha = DateTime.UtcNow
        });
        await _storage.SetAsync(SyncStateKey, estado);
    }

    private async Task ProcesarPendientesEliminarAsync()
    {
        var estado = await ObtenerEstadoSyncAsync();
        if (estado.PendientesEliminar.Count == 0) return;

        var procesados = new List<PendienteEliminarSync>();
        foreach (var pendiente in estado.PendientesEliminar)
        {
            try
            {
                switch (pendiente.Tabla)
                {
                    case "gastos": await _supabase.EliminarAsync<Gasto>("gastos", pendiente.Id); break;
                    case "categorias": await _supabase.EliminarAsync<Categoria>("categorias", pendiente.Id); break;
                    case "presupuestos": await _supabase.EliminarAsync<Presupuesto>("presupuestos", pendiente.Id); break;
                    case "recurrencias": await _supabase.EliminarAsync<Recurrencia>("recurrencias", pendiente.Id); break;
                    case "financiamientos": await _supabase.EliminarAsync<Financiamiento>("financiamientos", pendiente.Id); break;
                    case "perfiles": await _supabase.EliminarAsync<PerfilRecord>("perfiles", pendiente.Id); break;
                }
                procesados.Add(pendiente);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al eliminar pendiente de {Tabla} en la nube", pendiente.Tabla);
                _huboErrorSync = true;
            }
        }

        var idsProcesados = new HashSet<(string Tabla, Guid Id)>(procesados.Select(p => (p.Tabla, p.Id)));
        estado.PendientesEliminar.RemoveAll(p => idsProcesados.Contains((p.Tabla, p.Id)));
        await _storage.SetAsync(SyncStateKey, estado);
    }

    private async Task PushGastosAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null) return;

        List<Gasto> remotos;
        try
        {
            var filter = !string.IsNullOrEmpty(usuario.HogarId)
                ? $"hogar_id=eq.{Uri.EscapeDataString(usuario.HogarId!)}"
                : $"usuario_id=eq.{Uri.EscapeDataString(GetUserId(usuario))}";
            remotos = await _supabase.ObtenerTodosAsync<Gasto>("gastos", filter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener gastos remotos para sincronización");
            _huboErrorSync = true;
            return;
        }

        var todos = await _storage.GetAsync<List<Gasto>>("cdg_gastos") ?? new List<Gasto>();
        var pendientes = todos.Where(g => !g.Sincronizado).ToList();
        var remotosPorId = remotos.ToDictionary(r => r.Id);
        var indicesPorId = todos.Select((g, i) => (g.Id, i)).ToDictionary(x => x.Id, x => x.i);
        var errores = 0;

        foreach (var gasto in pendientes)
        {
            var versionOriginal = gasto.NumeroVersion;
            try
            {
                if (remotosPorId.TryGetValue(gasto.Id, out var remoto))
                {
                    if (remoto.NumeroVersion > gasto.NumeroVersion)
                    {
                        var tiempoLocal = gasto.UpdatedAt ?? gasto.ActualizadoEn ?? gasto.CreadoEn;
                        var tiempoRemoto = remoto.UpdatedAt ?? remoto.ActualizadoEn ?? remoto.CreadoEn;
                        if (tiempoRemoto > tiempoLocal)
                        {
                            gasto.Sincronizado = true;
                            continue;
                        }
                        gasto.NumeroVersion = remoto.NumeroVersion + 1;
                    }
                    else
                    {
                        gasto.NumeroVersion++;
                    }
                    var uidOriginal = gasto.UsuarioId;
                    if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                        gasto.UsuarioId = Guid.Parse(usuario.SupabaseUserId);
                    var actualizado = await _supabase.ActualizarAsync("gastos", gasto.Id, gasto);
                    gasto.UsuarioId = uidOriginal;
                    if (actualizado is not null)
                        gasto.UpdatedAt = actualizado.UpdatedAt;
                }
                else
                {
                    var uidOriginal = gasto.UsuarioId;
                    if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                        gasto.UsuarioId = Guid.Parse(usuario.SupabaseUserId);
                    var creado = await _supabase.GuardarAsync("gastos", gasto);
                    gasto.UsuarioId = uidOriginal;
                    if (creado is not null)
                        gasto.UpdatedAt = creado.UpdatedAt;
                }
                gasto.Sincronizado = true;
                if (indicesPorId.TryGetValue(gasto.Id, out var idx))
                    todos[idx] = gasto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al sincronizar gasto con la nube");
                gasto.NumeroVersion = versionOriginal;
                gasto.Sincronizado = false;
                errores++;
            }
        }

        await _storage.SetAsync("cdg_gastos", todos);
        if (errores > 0)
            _huboErrorSync = true;
    }

    private async Task PullGastosAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null) return;

        List<Gasto> remotos;
        try
        {
            var filter = !string.IsNullOrEmpty(usuario.HogarId)
                ? $"hogar_id=eq.{Uri.EscapeDataString(usuario.HogarId!)}"
                : $"usuario_id=eq.{Uri.EscapeDataString(GetUserId(usuario))}";
            remotos = await _supabase.ObtenerTodosAsync<Gasto>("gastos", filter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener gastos remotos para sincronización");
            _huboErrorSync = true;
            return;
        }

        var locales = await _storage.GetAsync<List<Gasto>>("cdg_gastos") ?? new List<Gasto>();
        var idsRemotos = new HashSet<Guid>(remotos.Select(r => r.Id));

        if (!string.IsNullOrEmpty(usuario.HogarId))
            locales.RemoveAll(l => l.Sincronizado && l.HogarId == usuario.HogarId && !idsRemotos.Contains(l.Id));
        else
            locales.RemoveAll(l => l.Sincronizado && l.UsuarioId == usuario.Id && !idsRemotos.Contains(l.Id));

        var localesPorId = new Dictionary<Guid, (Gasto item, int idx)>();
        for (int i = 0; i < locales.Count; i++)
            localesPorId[locales[i].Id] = (locales[i], i);

        foreach (var remoto in remotos)
        {
            if (localesPorId.TryGetValue(remoto.Id, out var encontrado))
            {
                var (local, idx) = encontrado;
                var tiempoRemoto = remoto.UpdatedAt ?? remoto.ActualizadoEn ?? remoto.CreadoEn;
                var tiempoLocal = local.UpdatedAt ?? local.ActualizadoEn ?? local.CreadoEn;
                if (tiempoRemoto > tiempoLocal)
                {
                    remoto.Sincronizado = true;
                    locales[idx] = remoto;
                    localesPorId[remoto.Id] = (remoto, idx);
                }
            }
            else
            {
                remoto.Sincronizado = true;
                localesPorId[remoto.Id] = (remoto, locales.Count);
                locales.Add(remoto);
            }
        }

        await _storage.SetAsync("cdg_gastos", locales);
    }

    private async Task PushCategoriasAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null) return;

        List<Categoria> remotos;
        try
        {
            var filter = !string.IsNullOrEmpty(usuario.HogarId)
                ? $"hogar_id=eq.{Uri.EscapeDataString(usuario.HogarId!)}"
                : $"usuario_id=eq.{Uri.EscapeDataString(GetUserId(usuario))}";
            remotos = await _supabase.ObtenerTodosAsync<Categoria>("categorias", filter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener categorías remotas para sincronización");
            _huboErrorSync = true;
            return;
        }

        foreach (var r in remotos)
            r.UsuarioId = usuario.Id;

        var categorias = await _categoriaService.ObtenerCategoriasAsync();
        var idsRemotos = new HashSet<Guid>(remotos.Select(r => r.Id));
        var errores = 0;

        foreach (var cat in categorias)
        {
            try
            {
                var uidOriginal = cat.UsuarioId;
                if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                    cat.UsuarioId = Guid.Parse(usuario.SupabaseUserId);

                if (idsRemotos.Contains(cat.Id))
                    await _supabase.ActualizarAsync("categorias", cat.Id, cat);
                else
                    await _supabase.GuardarAsync("categorias", cat);

                cat.UsuarioId = uidOriginal;
            }
            catch (Exception ex) { _logger.LogError(ex, "Error al sincronizar categoría con la nube"); errores++; }
        }

        if (errores > 0)
            _huboErrorSync = true;
    }

    private async Task PullCategoriasAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null) return;

        List<Categoria> remotos;
        try
        {
            var filter = !string.IsNullOrEmpty(usuario.HogarId)
                ? $"hogar_id=eq.{Uri.EscapeDataString(usuario.HogarId!)}"
                : $"usuario_id=eq.{Uri.EscapeDataString(GetUserId(usuario))}";
            remotos = await _supabase.ObtenerTodosAsync<Categoria>("categorias", filter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener categorías remotas para sincronización");
            _huboErrorSync = true;
            return;
        }
        foreach (var r in remotos)
            r.UsuarioId = usuario.Id;

        var locales = await _storage.GetAsync<List<Categoria>>("cdg_categorias") ?? new List<Categoria>();
        var localesPorId = new Dictionary<Guid, (Categoria item, int idx)>();
        for (int i = 0; i < locales.Count; i++)
            localesPorId[locales[i].Id] = (locales[i], i);

        foreach (var remoto in remotos)
        {
            if (localesPorId.TryGetValue(remoto.Id, out var encontrado))
            {
                var (local, idx) = encontrado;
                var tiempoRemoto = remoto.UpdatedAt ?? remoto.ActualizadoEn;
                var tiempoLocal = local.UpdatedAt ?? local.ActualizadoEn;
                if (tiempoRemoto > tiempoLocal)
                {
                    locales[idx] = remoto;
                    localesPorId[remoto.Id] = (remoto, idx);
                }
            }
            else
            {
                remoto.UsuarioId = usuario.Id;
                localesPorId[remoto.Id] = (remoto, locales.Count);
                locales.Add(remoto);
            }
        }

        await _storage.SetAsync("cdg_categorias", locales);
    }

    private async Task PushPresupuestosAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null) return;

        List<Presupuesto> remotos;
        try
        {
            var filter = !string.IsNullOrEmpty(usuario.HogarId)
                ? $"hogar_id=eq.{Uri.EscapeDataString(usuario.HogarId!)}"
                : $"usuario_id=eq.{Uri.EscapeDataString(GetUserId(usuario))}";
            remotos = await _supabase.ObtenerTodosAsync<Presupuesto>("presupuestos", filter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener presupuestos remotos para sincronización");
            _huboErrorSync = true;
            return;
        }

        foreach (var r in remotos)
            r.UsuarioId = usuario.Id;

        var presupuestos = await _presupuestoService.ObtenerPresupuestosAsync();
        var idsRemotos = new HashSet<Guid>(remotos.Select(r => r.Id));
        var errores = 0;

        foreach (var p in presupuestos)
        {
            try
            {
                var uidOriginal = p.UsuarioId;
                if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                    p.UsuarioId = Guid.Parse(usuario.SupabaseUserId);

                if (idsRemotos.Contains(p.Id))
                    await _supabase.ActualizarAsync("presupuestos", p.Id, p);
                else
                    await _supabase.GuardarAsync("presupuestos", p);

                p.UsuarioId = uidOriginal;
            }
            catch (Exception ex) { _logger.LogError(ex, "Error al sincronizar presupuesto con la nube"); errores++; }
        }

        if (errores > 0)
            _huboErrorSync = true;
    }

    private async Task PullPresupuestosAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null) return;

        List<Presupuesto> remotos;
        try
        {
            var filter = !string.IsNullOrEmpty(usuario.HogarId)
                ? $"hogar_id=eq.{Uri.EscapeDataString(usuario.HogarId!)}"
                : $"usuario_id=eq.{Uri.EscapeDataString(GetUserId(usuario))}";
            remotos = await _supabase.ObtenerTodosAsync<Presupuesto>("presupuestos", filter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener presupuestos remotos para sincronización");
            _huboErrorSync = true;
            return;
        }

        foreach (var r in remotos)
            r.UsuarioId = usuario.Id;

        var locales = await _storage.GetAsync<List<Presupuesto>>("cdg_presupuestos") ?? new List<Presupuesto>();
        var localesPorId = new Dictionary<Guid, (Presupuesto item, int idx)>();
        for (int i = 0; i < locales.Count; i++)
            localesPorId[locales[i].Id] = (locales[i], i);

        foreach (var remoto in remotos)
        {
            if (localesPorId.TryGetValue(remoto.Id, out var encontrado))
            {
                var (local, idx) = encontrado;
                var tiempoRemoto = remoto.UpdatedAt ?? remoto.ActualizadoEn;
                var tiempoLocal = local.UpdatedAt ?? local.ActualizadoEn;
                if (tiempoRemoto > tiempoLocal)
                {
                    locales[idx] = remoto;
                    localesPorId[remoto.Id] = (remoto, idx);
                }
            }
            else
            {
                localesPorId[remoto.Id] = (remoto, locales.Count);
                locales.Add(remoto);
            }
        }

        await _storage.SetAsync("cdg_presupuestos", locales);
    }

    private async Task PushRecurrenciasAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null) return;

        List<Recurrencia> remotos;
        try
        {
            var filter = !string.IsNullOrEmpty(usuario.HogarId)
                ? $"hogar_id=eq.{Uri.EscapeDataString(usuario.HogarId!)}"
                : $"usuario_id=eq.{Uri.EscapeDataString(GetUserId(usuario))}";
            remotos = await _supabase.ObtenerTodosAsync<Recurrencia>("recurrencias", filter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener recurrencias remotas para sincronización");
            _huboErrorSync = true;
            return;
        }

        foreach (var r in remotos)
            r.UsuarioId = usuario.Id;

        var recurrencias = await _recurrenciaService.ObtenerRecurrenciasAsync();
        var idsRemotos = new HashSet<Guid>(remotos.Select(r => r.Id));
        var errores = 0;

        foreach (var rec in recurrencias)
        {
            try
            {
                var uidOriginal = rec.UsuarioId;
                if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                    rec.UsuarioId = Guid.Parse(usuario.SupabaseUserId);

                if (idsRemotos.Contains(rec.Id))
                    await _supabase.ActualizarAsync("recurrencias", rec.Id, rec);
                else
                    await _supabase.GuardarAsync("recurrencias", rec);

                rec.UsuarioId = uidOriginal;
            }
            catch (Exception ex) { _logger.LogError(ex, "Error al sincronizar recurrencia con la nube"); errores++; }
        }

        if (errores > 0)
            _huboErrorSync = true;
    }

    private async Task PullRecurrenciasAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null) return;

        List<Recurrencia> remotos;
        try
        {
            var filter = !string.IsNullOrEmpty(usuario.HogarId)
                ? $"hogar_id=eq.{Uri.EscapeDataString(usuario.HogarId!)}"
                : $"usuario_id=eq.{Uri.EscapeDataString(GetUserId(usuario))}";
            remotos = await _supabase.ObtenerTodosAsync<Recurrencia>("recurrencias", filter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener recurrencias remotas para sincronización");
            _huboErrorSync = true;
            return;
        }

        foreach (var r in remotos)
            r.UsuarioId = usuario.Id;

        var locales = await _storage.GetAsync<List<Recurrencia>>("cdg_recurrencias") ?? new List<Recurrencia>();
        var localesPorId = new Dictionary<Guid, (Recurrencia item, int idx)>();
        for (int i = 0; i < locales.Count; i++)
            localesPorId[locales[i].Id] = (locales[i], i);

        foreach (var remoto in remotos)
        {
            if (localesPorId.TryGetValue(remoto.Id, out var encontrado))
            {
                var (local, idx) = encontrado;
                var tiempoRemoto = remoto.UpdatedAt ?? remoto.ActualizadoEn;
                var tiempoLocal = local.UpdatedAt ?? local.ActualizadoEn;
                if (tiempoRemoto > tiempoLocal)
                {
                    locales[idx] = remoto;
                    localesPorId[remoto.Id] = (remoto, idx);
                }
            }
            else
            {
                localesPorId[remoto.Id] = (remoto, locales.Count);
                locales.Add(remoto);
            }
        }

        await _storage.SetAsync("cdg_recurrencias", locales);
    }

    private async Task PushFinanciamientosAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null) return;

        List<Financiamiento> remotos;
        try
        {
            var filter = !string.IsNullOrEmpty(usuario.HogarId)
                ? $"hogar_id=eq.{Uri.EscapeDataString(usuario.HogarId!)}"
                : $"usuario_id=eq.{Uri.EscapeDataString(GetUserId(usuario))}";
            remotos = await _supabase.ObtenerTodosAsync<Financiamiento>("financiamientos", filter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener financiamientos remotos para sincronización");
            _huboErrorSync = true;
            return;
        }

        foreach (var r in remotos)
            r.UsuarioId = usuario.Id;

        var financiamientos = await _financiamientoService.ObtenerFinanciamientosAsync();
        var idsRemotos = new HashSet<Guid>(remotos.Select(r => r.Id));
        var errores = 0;

        foreach (var item in financiamientos)
        {
            try
            {
                var uidOriginal = item.UsuarioId;
                if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                    item.UsuarioId = Guid.Parse(usuario.SupabaseUserId);

                if (idsRemotos.Contains(item.Id))
                    await _supabase.ActualizarAsync("financiamientos", item.Id, item);
                else
                    await _supabase.GuardarAsync("financiamientos", item);

                item.UsuarioId = uidOriginal;
            }
            catch (Exception ex) { _logger.LogError(ex, "Error al sincronizar financiamiento con la nube"); errores++; }
        }

        if (errores > 0)
            _huboErrorSync = true;
    }

    private async Task PullFinanciamientosAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null) return;

        List<Financiamiento> remotos;
        try
        {
            var filter = !string.IsNullOrEmpty(usuario.HogarId)
                ? $"hogar_id=eq.{Uri.EscapeDataString(usuario.HogarId!)}"
                : $"usuario_id=eq.{Uri.EscapeDataString(GetUserId(usuario))}";
            remotos = await _supabase.ObtenerTodosAsync<Financiamiento>("financiamientos", filter);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener financiamientos remotos para sincronización");
            _huboErrorSync = true;
            return;
        }

        foreach (var r in remotos)
            r.UsuarioId = usuario.Id;

        var locales = await _storage.GetAsync<List<Financiamiento>>("cdg_financiamientos") ?? new List<Financiamiento>();
        var localesPorId = new Dictionary<Guid, (Financiamiento item, int idx)>();
        for (int i = 0; i < locales.Count; i++)
            localesPorId[locales[i].Id] = (locales[i], i);

        foreach (var remoto in remotos)
        {
            if (localesPorId.TryGetValue(remoto.Id, out var encontrado))
            {
                var (local, idx) = encontrado;
                var tiempoRemoto = remoto.UpdatedAt ?? remoto.ActualizadoEn;
                var tiempoLocal = local.UpdatedAt ?? local.ActualizadoEn;
                if (tiempoRemoto > tiempoLocal)
                {
                    locales[idx] = remoto;
                    localesPorId[remoto.Id] = (remoto, idx);
                }
            }
            else
            {
                localesPorId[remoto.Id] = (remoto, locales.Count);
                locales.Add(remoto);
            }
        }

        await _storage.SetAsync("cdg_financiamientos", locales);
    }

    private static string GetUserId(Usuario usuario)
    {
        return !string.IsNullOrEmpty(usuario.SupabaseUserId) ? usuario.SupabaseUserId : usuario.Id.ToString();
    }

    private async Task PushPerfilesAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null) return;

        if (usuario.Sincronizado) return;

        try
        {
            var uid = GetUserId(usuario);
            var remotos = await _supabase.ObtenerTodosAsync<PerfilRecord>("perfiles", $"id=eq.{Uri.EscapeDataString(uid)}") ?? new List<PerfilRecord>();
            var perfil = new PerfilRecord
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Moneda = usuario.Moneda,
                ModoGamificadoActivo = usuario.ModoGamificadoActivo,
                ExcluirRecurrentesDePresupuesto = usuario.ExcluirRecurrentesDePresupuesto,
                ExcluirCreditosDePresupuesto = usuario.ExcluirCreditosDePresupuesto,
                PinDelaySegundos = usuario.PinDelaySegundos
            };

            if (remotos.Count > 0)
            {
                var actualizado = await _supabase.ActualizarAsync("perfiles", usuario.Id, perfil);
                if (actualizado is not null)
                    usuario.UpdatedAt = actualizado.UpdatedAt;
            }
            else
            {
                var creado = await _supabase.GuardarAsync("perfiles", perfil);
                if (creado is not null)
                    usuario.UpdatedAt = creado.UpdatedAt;
            }

            usuario.Sincronizado = true;
            await _storage.SetAsync("cdg_usuario", usuario);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al sincronizar perfil con la nube");
            _huboErrorSync = true;
        }
    }

    private async Task PullPerfilesAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null) return;

        List<PerfilRecord> remotos;
        try
        {
            remotos = await _supabase.ObtenerTodosAsync<PerfilRecord>("perfiles", $"id=eq.{usuario.Id}") ?? new List<PerfilRecord>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener perfil remoto para sincronización");
            _huboErrorSync = true;
            return;
        }

        if (remotos.Count == 0) return;
        var remoto = remotos[0];

        var tiempoRemoto = remoto.UpdatedAt;
        var tiempoLocal = usuario.UpdatedAt ?? usuario.FechaRegistro;
        if (tiempoRemoto > tiempoLocal)
        {
            if (remoto.Nombre is not null) usuario.Nombre = remoto.Nombre;
            if (remoto.Moneda is not null) usuario.Moneda = remoto.Moneda;
            if (remoto.ModoGamificadoActivo.HasValue) usuario.ModoGamificadoActivo = remoto.ModoGamificadoActivo.Value;
            if (remoto.ExcluirRecurrentesDePresupuesto.HasValue) usuario.ExcluirRecurrentesDePresupuesto = remoto.ExcluirRecurrentesDePresupuesto.Value;
            if (remoto.ExcluirCreditosDePresupuesto.HasValue) usuario.ExcluirCreditosDePresupuesto = remoto.ExcluirCreditosDePresupuesto.Value;
            if (remoto.PinDelaySegundos.HasValue) usuario.PinDelaySegundos = remoto.PinDelaySegundos.Value;
            usuario.UpdatedAt = remoto.UpdatedAt;
            usuario.Sincronizado = true;
            await _storage.SetAsync("cdg_usuario", usuario);
        }
    }
}
