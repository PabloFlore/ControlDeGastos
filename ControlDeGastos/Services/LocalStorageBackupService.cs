using System.Text.Json;
using ControlDeGastos.Models;
using Microsoft.JSInterop;

namespace ControlDeGastos.Services;

public class LocalStorageBackupService : ILocalStorageBackupService
{
    private readonly IJSRuntime _js;
    private const string BackupKey = "cdg_licencia_backup";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public LocalStorageBackupService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task GuardarLicenciaAsync(Licencia licencia)
    {
        try
        {
            var json = JsonSerializer.Serialize(licencia, JsonOptions);
            await _js.InvokeVoidAsync("localStorage.setItem", BackupKey, json);
        }
        catch
        {
        }
    }

    public async Task<Licencia?> CargarLicenciaAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("localStorage.getItem", BackupKey);
            if (json is null) return null;
            return JsonSerializer.Deserialize<Licencia>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task LimpiarAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.removeItem", BackupKey);
        }
        catch
        {
        }
    }
}
