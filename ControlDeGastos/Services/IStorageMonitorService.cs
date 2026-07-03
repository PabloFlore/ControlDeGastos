namespace ControlDeGastos.Services;

public class DetalleStorage
{
    public string Key { get; set; } = "";
    public long TamanioBytes { get; set; }
    public int CantidadRegistros { get; set; }
}

public interface IStorageMonitorService
{
    Task<(double usedMB, double quotaMB)> EstimarAsync();
    Task<double> ObtenerPorcentajeUsadoAsync();
    Task<List<DetalleStorage>> ObtenerDetalleAsync();
}
