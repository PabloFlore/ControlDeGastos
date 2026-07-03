using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface IRecurrenciaService
{
    Task<List<Recurrencia>> ObtenerRecurrenciasAsync();
    Task<Recurrencia> CrearRecurrenciaAsync(Recurrencia recurrencia);
    Task ActualizarRecurrenciaAsync(Recurrencia recurrencia);
    Task EliminarRecurrenciaAsync(Guid id);
    Task EliminarRecurrenciaConGastosAsync(Guid id);
    Task<List<Gasto>> GenerarPendientesAsync();
    Task MigrarRecurrenciasAHogarAsync(string hogarId);
}
