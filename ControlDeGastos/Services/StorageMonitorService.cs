using Microsoft.JSInterop;

namespace ControlDeGastos.Services;

public class StorageMonitorService : IStorageMonitorService
{
    private readonly IJSRuntime _js;

    public StorageMonitorService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<(double usedMB, double quotaMB)> EstimarAsync()
    {
        try
        {
            var result = await _js.InvokeAsync<StorageEstimateResult>("window.storageMonitor.estimar");
            var usedMB = result.Usage / (1024.0 * 1024.0);
            var quotaMB = result.Quota / (1024.0 * 1024.0);
            return (usedMB, quotaMB);
        }
        catch
        {
            return (0, 5);
        }
    }

    public async Task<double> ObtenerPorcentajeUsadoAsync()
    {
        var (used, quota) = await EstimarAsync();
        if (quota <= 0) return 0;
        return (used / quota) * 100.0;
    }

    public async Task<List<DetalleStorage>> ObtenerDetalleAsync()
    {
        var detalles = new List<DetalleStorage>();

        try
        {
            var keys = await _js.InvokeAsync<string[]>("window.storageMonitor.obtenerTodasLasClaves");

            foreach (var key in keys)
            {
                var size = await _js.InvokeAsync<long>("window.storageMonitor.obtenerTamanioClave", key);
                var count = await _js.InvokeAsync<int>("window.storageMonitor.obtenerRegistros", key);
                detalles.Add(new DetalleStorage
                {
                    Key = key,
                    TamanioBytes = size,
                    CantidadRegistros = count,
                });
            }
        }
        catch
        {
        }

        return detalles;
    }

    private class StorageEstimateResult
    {
        public long Usage { get; set; }
        public long Quota { get; set; }
    }
}
