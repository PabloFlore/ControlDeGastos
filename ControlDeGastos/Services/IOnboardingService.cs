namespace ControlDeGastos.Services;

public interface IOnboardingService
{
    Task<bool> EstaCompletadoAsync();
    Task CompletarAsync();
    Task SaltarAsync();
}
