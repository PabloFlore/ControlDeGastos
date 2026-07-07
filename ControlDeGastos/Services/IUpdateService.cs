namespace ControlDeGastos.Services;

public interface IUpdateService
{
    Task<bool> HayActualizacionAsync();
    Task<bool> EstaPospuestoAsync();
    Task<bool> PosponerAsync();
    Task OlvidarAsync();
    Task ActivarAsync();
}
