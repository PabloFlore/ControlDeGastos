namespace ControlDeGastos.Services;

public class OnboardingService : IOnboardingService
{
    private const string StorageKey = "cdg_onboarding_completado";
    private readonly IStorageService _storage;

    public OnboardingService(IStorageService storage)
    {
        _storage = storage;
    }

    public async Task<bool> EstaCompletadoAsync()
    {
        return await _storage.KeyExistsAsync(StorageKey);
    }

    public async Task CompletarAsync()
    {
        await _storage.SetAsync(StorageKey, true);
    }

    public async Task SaltarAsync()
    {
        await CompletarAsync();
    }
}
