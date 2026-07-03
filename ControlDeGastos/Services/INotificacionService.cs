using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface INotificacionService
{
    Task<List<Notificacion>> VerificarNotificacionesAsync();
}
