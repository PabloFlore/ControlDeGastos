using Microsoft.JSInterop;

namespace ControlDeGastos.Services;

public class UpdateService : IUpdateService
{
    private readonly IJSRuntime _js;

    public UpdateService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task ForzarRecargaAsync()
    {
        await _js.InvokeVoidAsync("forzarRecarga");
    }

    public async Task<bool> HayActualizacionPendienteAsync()
    {
        try
        {
            return await _js.InvokeAsync<bool>("verificarActualizacionPendiente");
        }
        catch
        {
            return false;
        }
    }
}
