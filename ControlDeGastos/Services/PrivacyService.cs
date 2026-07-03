namespace ControlDeGastos.Services;

public class PrivacyService : IPrivacyService
{
    private const string StorageKey = "cdg_privacy_accepted";
    private readonly IStorageService _storage;

    public PrivacyService(IStorageService storage)
    {
        _storage = storage;
    }

    public async Task<bool> HaAceptadoAsync()
    {
        return await _storage.KeyExistsAsync(StorageKey);
    }

    public async Task AceptarAsync()
    {
        await _storage.SetAsync(StorageKey, true);
    }
}
