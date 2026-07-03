using System.Text.Json;
using Microsoft.JSInterop;

namespace ControlDeGastos.Services;

public class IndexedDbStorageService : IStorageService
{
    private readonly IJSRuntime _js;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public IndexedDbStorageService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("window.indexedDbStorage.getItem", key);
            if (json is null) return default;
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        await _js.InvokeVoidAsync("window.indexedDbStorage.setItem", key, json);
    }

    public async Task RemoveAsync(string key)
    {
        await _js.InvokeVoidAsync("window.indexedDbStorage.removeItem", key);
    }

    public async Task ClearAsync()
    {
        await _js.InvokeVoidAsync("window.indexedDbStorage.clear");
    }

    public async Task<bool> KeyExistsAsync(string key)
    {
        return await _js.InvokeAsync<bool>("window.indexedDbStorage.keyExists", key);
    }
}
