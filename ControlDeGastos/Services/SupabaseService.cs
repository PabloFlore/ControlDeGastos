using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Supabase;
using Client = Supabase.Client;

namespace ControlDeGastos.Services;

public class SupabaseService : ISupabaseService
{
    private readonly IStorageService _storage;
    private readonly HttpClient _http;
    private Client? _client;
    private readonly string _url = SupabaseConfig.Url;
    private readonly string _key = SupabaseConfig.AnonKey;
    private readonly ILogger<SupabaseService> _logger;
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public SupabaseService(IStorageService storage, HttpClient http, ILogger<SupabaseService> logger)
    {
        _storage = storage;
        _http = http;
        _logger = logger;
    }

    private static async Task<T> WithRetryAsync<T>(Func<Task<T>> action, ILogger logger, string operation, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try { return await action(); }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex, "Intento {Attempt}/{MaxRetries} falló para {Operation}. Reintentando...", attempt, maxRetries, operation);
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 200));
            }
        }
        return await action();
    }

    private static async Task WithRetryAsync(Func<Task> action, ILogger logger, string operation, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try { await action(); return; }
            catch (Exception ex) when (attempt < maxRetries)
            {
                logger.LogWarning(ex, "Intento {Attempt}/{MaxRetries} falló para {Operation}. Reintentando...", attempt, maxRetries, operation);
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 200));
            }
        }
        await action();
    }

    public async Task InicializarAsync()
    {
        if (_client != null) return;

        await InitLock.WaitAsync();
        try
        {
            if (_client != null) return;

            var options = new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = false
            };

            _client = new Client(_url, _key, options);
            await _client.InitializeAsync();
        }
        finally { InitLock.Release(); }
    }

    public async Task<bool> EstaConectadoAsync()
    {
        await InicializarAsync();
        if (_client == null) return false;

        try { return _client.Auth.CurrentSession != null; }
        catch (Exception ex) { _logger.LogWarning(ex, "Error al verificar conexión con Supabase"); return false; }
    }

    public async Task<bool> IniciarSesionAsync(string email, string password)
    {
        await InicializarAsync();
        if (_client == null) return false;

        try
        {
            var session = await _client.Auth.SignIn(email, password);
            if (session != null)
            {
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al iniciar sesión en Supabase");
            return false;
        }
    }

    public async Task<string?> ObtenerEmailSesionAsync()
    {
        await InicializarAsync();
        if (_client == null) return null;

        try
        {
            var user = _client.Auth.CurrentUser;
            var email = user?.Email;
            _logger?.LogInformation("ObtenerEmailSesionAsync: user={User={HasUser}, email={Email}", user != null, email);
            return email;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener email de la sesión");
            return null;
        }
    }

    public async Task<string?> ObtenerUsuarioIdAsync()
    {
        await InicializarAsync();
        if (_client == null) return null;

        try
        {
            var user = _client.Auth.CurrentUser;
            var id = user?.Id;
            _logger?.LogInformation("ObtenerUsuarioIdAsync: id={Id}", id);
            return id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener ID de usuario Supabase");
            return null;
        }
    }

    public async Task CerrarSesionAsync()
    {
        await InicializarAsync();
        if (_client != null)
        {
                try { await _client.Auth.SignOut(); }
                catch (Exception ex) { _logger.LogWarning(ex, "Error al cerrar sesión en Supabase"); }
            }
    }

    public async Task<List<T>> ObtenerTodosAsync<T>(string tabla, string? filter = null, int? limit = null, int? offset = null) where T : class
    {
        await InicializarAsync();
        if (_client == null)
            return new List<T>();

        return await WithRetryAsync(async () =>
        {
            var url = $"{_url}/rest/v1/{tabla}?select=*";
            if (!string.IsNullOrEmpty(filter))
                url += $"&{filter}";
            if (limit.HasValue)
                url += $"&limit={limit.Value}";
            if (offset.HasValue)
                url += $"&offset={offset.Value}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("apikey", _key);
            if (_client!.Auth.CurrentSession?.AccessToken is { } token)
                request.Headers.Authorization = new("Bearer", token);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
        }, _logger, $"{tabla}/GET");
    }

    public async Task<T> GuardarAsync<T>(string tabla, T item) where T : class
    {
        await InicializarAsync();
        if (_client == null)
            return item;

        return await WithRetryAsync(async () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/{tabla}")
            {
                Content = JsonContent.Create(item, options: JsonOptions),
            };
            request.Headers.Add("apikey", _key);
            request.Headers.Add("Prefer", "return=representation");
            if (_client!.Auth.CurrentSession?.AccessToken is { } token)
                request.Headers.Authorization = new("Bearer", token);

            var response = await _http.SendAsync(request);
            _logger?.LogInformation("GuardarAsync {Tabla}: Status={Status}", tabla, response.StatusCode);
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogError("GuardarAsync {Tabla} FAILED: {Error}", tabla, errorContent);
            }
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var created = JsonSerializer.Deserialize<List<T>>(json, JsonOptions);
            return created is { Count: > 0 } ? created[0] : item;
        }, _logger, $"{tabla}/POST");
    }

    public async Task EliminarAsync<T>(string tabla, object id) where T : class
    {
        await InicializarAsync();
        if (_client == null)
            return;

        try
        {
            await WithRetryAsync(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{_url}/rest/v1/{tabla}?id=eq.{Uri.EscapeDataString(id.ToString()!)}");
                request.Headers.Add("apikey", _key);
                if (_client!.Auth.CurrentSession?.AccessToken is { } token)
                    request.Headers.Authorization = new("Bearer", token);

                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
            }, _logger, $"{tabla}/DELETE");
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Error al eliminar en Supabase"); }
    }

    public async Task EliminarConFiltroAsync<T>(string tabla, string filter) where T : class
    {
        await InicializarAsync();
        if (_client == null)
            return;

        try
        {
            await WithRetryAsync(async () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, $"{_url}/rest/v1/{tabla}?{filter}");
                request.Headers.Add("apikey", _key);
                if (_client!.Auth.CurrentSession?.AccessToken is { } token)
                    request.Headers.Authorization = new("Bearer", token);

                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
            }, _logger, $"{tabla}/DELETE_FILTER");
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Error al eliminar con filtro en Supabase"); }
    }

    public async Task<T> ActualizarAsync<T>(string tabla, object id, T item) where T : class
    {
        await InicializarAsync();
        if (_client == null)
            return item;

        return await WithRetryAsync(async () =>
        {
            var request = new HttpRequestMessage(HttpMethod.Patch, $"{_url}/rest/v1/{tabla}?id=eq.{Uri.EscapeDataString(id.ToString()!)}")
            {
                Content = JsonContent.Create(item, options: JsonOptions),
            };
            request.Headers.Add("apikey", _key);
            request.Headers.Add("Prefer", "return=representation");
            if (_client!.Auth.CurrentSession?.AccessToken is { } token)
                request.Headers.Authorization = new("Bearer", token);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var updated = JsonSerializer.Deserialize<List<T>>(json, JsonOptions);
            return updated is { Count: > 0 } ? updated[0] : item;
        }, _logger, $"{tabla}/PATCH");
    }

}
