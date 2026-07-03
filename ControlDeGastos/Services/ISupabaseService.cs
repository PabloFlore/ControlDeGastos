namespace ControlDeGastos.Services;

public interface ISupabaseService
{
    Task InicializarAsync();
    Task<bool> EstaConectadoAsync();
    Task<bool> IniciarSesionAsync(string email, string password);
    Task CerrarSesionAsync();
    Task<string?> ObtenerEmailSesionAsync();
    Task<string?> ObtenerUsuarioIdAsync();
    Task<List<T>> ObtenerTodosAsync<T>(string tabla, string? filter = null, int? limit = null, int? offset = null) where T : class;
    Task<T> GuardarAsync<T>(string tabla, T item) where T : class;
    Task EliminarAsync<T>(string tabla, object id) where T : class;
    Task EliminarConFiltroAsync<T>(string tabla, string filter) where T : class;
    Task<T> ActualizarAsync<T>(string tabla, object id, T item) where T : class;
}
