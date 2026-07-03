namespace ControlDeGastos.Services;

public interface IPrivacyService
{
    Task<bool> HaAceptadoAsync();
    Task AceptarAsync();
}
