namespace ControlDeGastos.Services;

public interface IPinLockService
{
    Task<bool> EstaConfiguradoAsync();
    Task ConfigurarPinAsync(string pin);
    Task<bool> VerificarPinAsync(string pin);
    Task CambiarPinAsync(string pinViejo, string pinNuevo);
    Task DesactivarPinAsync(string pin);
    Task<int> ObtenerDelayBloqueoSegundosAsync();
    Task GuardarDelayBloqueoSegundosAsync(int segundos);
    bool SesionEstaAutenticada();
    void EstablecerSesionAutenticada();
    void CerrarSesion();
    Task<int> ObtenerIntentosFallidosAsync();
    Task<bool> EstaTemporalmenteBloqueadoAsync();
    Task<int> ObtenerTiempoEsperaRestanteSegundosAsync();
}
