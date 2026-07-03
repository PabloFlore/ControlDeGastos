using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class CategoriaService : ICategoriaService
{
    private const string StorageKey = "cdg_categorias";
    private readonly IStorageService _storage;
    private readonly IUsuarioService _usuarioService;
    private readonly ISupabaseService _supabaseService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CategoriaService> _logger;

    private static readonly List<Categoria> CategoriasPorDefecto = new()
    {
        new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"), Nombre = "Comida", Icono = "🍕", Color = "#ff6b6b", Tipo = TipoGasto.Gasto, Orden = 1 },
        new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000002"), Nombre = "Transporte", Icono = "🚗", Color = "#4ecdc4", Tipo = TipoGasto.Gasto, Orden = 2 },
        new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000003"), Nombre = "Vivienda", Icono = "🏠", Color = "#45b7d1", Tipo = TipoGasto.Gasto, Orden = 3 },
        new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000004"), Nombre = "Servicios", Icono = "💡", Color = "#f9ca24", Tipo = TipoGasto.Gasto, Orden = 4 },
        new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000005"), Nombre = "Salud", Icono = "💊", Color = "#6c5ce7", Tipo = TipoGasto.Gasto, Orden = 5 },
        new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000006"), Nombre = "Entretenimiento", Icono = "🎬", Color = "#fd79a8", Tipo = TipoGasto.Gasto, Orden = 6 },
        new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000007"), Nombre = "Ropa", Icono = "👕", Color = "#e17055", Tipo = TipoGasto.Gasto, Orden = 7 },
        new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000008"), Nombre = "Educación", Icono = "📚", Color = "#00b894", Tipo = TipoGasto.Gasto, Orden = 8 },
        new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000009"), Nombre = "Salario", Icono = "💰", Color = "#2ecc71", Tipo = TipoGasto.Ingreso, Orden = 9 },
        new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000010"), Nombre = "Freelance", Icono = "💻", Color = "#3498db", Tipo = TipoGasto.Ingreso, Orden = 10 },
        new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000011"), Nombre = "Inversiones", Icono = "📈", Color = "#9b59b6", Tipo = TipoGasto.Ingreso, Orden = 11 },
        new() { Id = Guid.Parse("a1000000-0000-0000-0000-000000000012"), Nombre = "Otros", Icono = "📦", Color = "#636e72", Tipo = TipoGasto.Gasto, Orden = 12 },
    };

    public CategoriaService(IStorageService storage, IUsuarioService usuarioService, ISupabaseService supabaseService, IServiceProvider serviceProvider, ILogger<CategoriaService> logger)
    {
        _storage = storage;
        _usuarioService = usuarioService;
        _supabaseService = supabaseService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<List<Categoria>> ObtenerCategoriasAsync()
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
                var remotos = await _supabaseService.ObtenerTodosAsync<Categoria>("categorias", filter);

                foreach (var r in remotos)
                    r.UsuarioId = usuario.Id;

                var locales = await _storage.GetAsync<List<Categoria>>(StorageKey) ?? new List<Categoria>();
                var idsRemotos = new HashSet<Guid>(remotos.Select(r => r.Id));

                var merged = new List<Categoria>(remotos);
                merged.AddRange(locales.Where(l => !idsRemotos.Contains(l.Id)));

                await _storage.SetAsync(StorageKey, merged);

                if (!string.IsNullOrEmpty(usuario.HogarId))
                    return merged.Where(c => c.HogarId == null || c.HogarId == usuario.HogarId).ToList();
                return merged.Where(c => c.UsuarioId == null || c.UsuarioId == usuario.Id).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al obtener categorías desde la nube, usando local");
            }
        }

        var categorias = await _storage.GetAsync<List<Categoria>>(StorageKey);
        if (categorias == null) return new List<Categoria>();

        if (!string.IsNullOrEmpty(usuario.HogarId))
            return categorias.Where(c => c.HogarId == null || c.HogarId == usuario.HogarId).ToList();
        return categorias.Where(c => c.UsuarioId == null || c.UsuarioId == usuario.Id).ToList();
    }

    public async Task MigrarCategoriasAHogarAsync(string hogarId)
    {
        var categorias = await _storage.GetAsync<List<Categoria>>(StorageKey) ?? new List<Categoria>();
        foreach (var c in categorias)
        {
            if (string.IsNullOrEmpty(c.HogarId))
                c.HogarId = hogarId;
        }
        await _storage.SetAsync(StorageKey, categorias);
    }

    public async Task<Categoria> CrearCategoriaAsync(Categoria categoria)
    {
        var categorias = await _storage.GetAsync<List<Categoria>>(StorageKey) ?? new List<Categoria>();
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        categoria.HogarId = usuario.HogarId;
        categoria.UsuarioId = usuario.Id;
        categoria.ActualizadoEn = DateTime.UtcNow;
        categorias.Add(categoria);
        await _storage.SetAsync(StorageKey, categorias);

        if (usuario.PlanActivo == PlanType.Nube)
        {
            try
            {
                var uidOriginal = categoria.UsuarioId;
                if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                    categoria.UsuarioId = Guid.Parse(usuario.SupabaseUserId);
                await _supabaseService.GuardarAsync("categorias", categoria);
                categoria.UsuarioId = uidOriginal;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al sincronizar categoría con la nube"); }
        }

        return categoria;
    }

    public async Task<Categoria> ActualizarCategoriaAsync(Categoria categoria)
    {
        var categorias = await _storage.GetAsync<List<Categoria>>(StorageKey) ?? new List<Categoria>();
        var idx = categorias.FindIndex(c => c.Id == categoria.Id);
        if (idx < 0) return categoria;

        categoria.ActualizadoEn = DateTime.UtcNow;
        categorias[idx] = categoria;
        await _storage.SetAsync(StorageKey, categorias);

        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario.PlanActivo == PlanType.Nube)
        {
            try
            {
                var uidOriginal = categoria.UsuarioId;
                if (!string.IsNullOrEmpty(usuario.SupabaseUserId))
                    categoria.UsuarioId = Guid.Parse(usuario.SupabaseUserId);
                await _supabaseService.ActualizarAsync("categorias", categoria.Id, categoria);
                categoria.UsuarioId = uidOriginal;
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Error al sincronizar categoría con la nube"); }
        }

        return categoria;
    }

    public async Task EliminarCategoriaAsync(Guid id)
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        var esNube = usuario.PlanActivo == PlanType.Nube;

        if (esNube)
        {
            var sync = _serviceProvider.GetRequiredService<ISyncService>();
            await sync.RegistrarPendienteEliminarAsync("categorias", id);
        }

        var categorias = await _storage.GetAsync<List<Categoria>>(StorageKey) ?? new List<Categoria>();
        categorias.RemoveAll(c => c.Id == id);
        await _storage.SetAsync(StorageKey, categorias);

        if (esNube)
        {
            try { await _supabaseService.EliminarAsync<Categoria>("categorias", id); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error al sincronizar eliminación de categoría con la nube");
            }
        }
    }

    public async Task InicializarCategoriasPorDefectoAsync()
    {
        var existe = await _storage.KeyExistsAsync(StorageKey);
        if (existe) return;

        await _storage.SetAsync(StorageKey, CategoriasPorDefecto);
    }
}
