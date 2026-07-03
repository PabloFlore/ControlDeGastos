namespace ControlDeGastos.Tests.Fakes;

public class InMemoryStorageService : IStorageService
{
    private readonly Dictionary<string, object> _store = new();

    public Task<T?> GetAsync<T>(string key)
    {
        if (_store.TryGetValue(key, out var value) && value is T typed)
            return Task.FromResult<T?>(typed);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value)
    {
        _store[key] = value!;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        _store.Clear();
        return Task.CompletedTask;
    }

    public Task<bool> KeyExistsAsync(string key)
    {
        return Task.FromResult(_store.ContainsKey(key));
    }
}
