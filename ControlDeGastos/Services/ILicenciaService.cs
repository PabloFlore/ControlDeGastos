using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface ILicenciaService
{
    Task<Licencia> ActivarLicenciaAsync(string token);
    Task<Licencia> ObtenerEstadoLicenciaAsync();
    Task<bool> VerificarYActualizarVigenciaAsync();
    (bool valido, TipoLicencia tipo, DateTime? expiracion, string mensaje, PlanType plan, bool modoGamificado) ValidarToken(string token);
    Task GuardarLicenciaLocalAsync(Licencia licencia);
}
