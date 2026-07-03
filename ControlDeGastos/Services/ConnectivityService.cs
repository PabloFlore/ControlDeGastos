using Microsoft.JSInterop;

namespace ControlDeGastos.Services;

public class ConnectivityService : IConnectivityService, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly ILogger<ConnectivityService> _logger;
    private bool _isOnline = true;
    private DotNetObjectReference<ConnectivityService>? _ref;
    private bool _initialized;

    public bool IsOnline => _isOnline;
    public event Action<bool>? ConnectivityChanged;

    public ConnectivityService(IJSRuntime js, ILogger<ConnectivityService> logger)
    {
        _js = js;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            _isOnline = await _js.InvokeAsync<bool>("eval", "navigator.onLine");
            _logger.LogInformation("Estado de conectividad inicial: {State}", _isOnline ? "online" : "offline");

            await _js.InvokeVoidAsync("eval", @"
                if (!window.__cdg_conn_init) {
                    window.__cdg_conn_init = true;
                    window.__cdg_setup_connectivity = function(ref) {
                        window.__cdg_conn_ref = ref;
                        window.addEventListener('online', function() {
                            try { ref.invokeMethodAsync('OnOnlineChanged', true); } catch(e) {}
                        });
                        window.addEventListener('offline', function() {
                            try { ref.invokeMethodAsync('OnOnlineChanged', false); } catch(e) {}
                        });
                    };
                }
            ");

            _ref = DotNetObjectReference.Create(this);
            await _js.InvokeVoidAsync("__cdg_setup_connectivity", _ref);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al inicializar ConnectivityService");
        }
    }

    [JSInvokable]
    public void OnOnlineChanged(bool online)
    {
        if (_isOnline == online) return;
        _isOnline = online;
        _logger.LogInformation("Conectividad cambiada: {State}", online ? "online" : "offline");
        ConnectivityChanged?.Invoke(online);
    }

    public async ValueTask DisposeAsync()
    {
        _ref?.Dispose();
        try { await _js.InvokeVoidAsync("eval", "window.__cdg_conn_ref = null;"); }
        catch { }
    }
}
