using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface ILocalStorageBackupService
{
    Task GuardarLicenciaAsync(Licencia licencia);
    Task<Licencia?> CargarLicenciaAsync();
    Task LimpiarAsync();
}
