namespace ControlDeGastos.Services;

public interface IUpdateService
{
    Task ForzarRecargaAsync();
    Task<bool> HayActualizacionPendienteAsync();
}
