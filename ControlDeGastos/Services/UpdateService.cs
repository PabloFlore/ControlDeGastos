using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace ControlDeGastos.Services;

public class UpdateService : IUpdateService
{
    private const string UpdateKey = "cdg_update_postpuesto";
    private readonly IJSRuntime _js;
    private readonly IStorageService _storage;
    private bool? _hayActualizacion;

    public UpdateService(IJSRuntime js, IStorageService storage)
    {
        _js = js;
        _storage = storage;
    }

    public async Task<bool> HayActualizacionAsync()
    {
        if (_hayActualizacion.HasValue)
            return _hayActualizacion.Value;

        try
        {
            await _js.InvokeVoidAsync("updateChecker.iniciar");
            _hayActualizacion = await _js.InvokeAsync<bool>("updateChecker.hayActualizacion");
        }
        catch
        {
            _hayActualizacion = false;
        }

        return _hayActualizacion.Value;
    }

    public async Task<bool> EstaPospuestoAsync()
    {
        return await _storage.GetAsync<bool>(UpdateKey);
    }

    public async Task<bool> PosponerAsync()
    {
        await _storage.SetAsync(UpdateKey, true);
        return true;
    }

    public async Task OlvidarAsync()
    {
        await _storage.RemoveAsync(UpdateKey);
        _hayActualizacion = null;
    }

    public async Task ActivarAsync()
    {
        await _js.InvokeVoidAsync("updateChecker.activar");
        _hayActualizacion = null;
        await OlvidarAsync();
    }
}
