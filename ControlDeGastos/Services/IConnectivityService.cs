namespace ControlDeGastos.Services;

public interface IConnectivityService
{
    bool IsOnline { get; }
    event Action<bool>? ConnectivityChanged;
    Task InitializeAsync();
}
