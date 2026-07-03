using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface IFinanciamientoService
{
    Task<List<Financiamiento>> ObtenerFinanciamientosAsync();
    Task<Financiamiento> CrearFinanciamientoAsync(Financiamiento item);
    Task<Financiamiento> ActualizarFinanciamientoAsync(Financiamiento item);
    Task EliminarFinanciamientoAsync(Guid id);
    Task EliminarFinanciamientoConGastosAsync(Guid id);
    Task<List<Gasto>> GenerarCuotasPendientesAsync();
    Task<List<string>> ObtenerBancosAsync();
    Task AgregarBancoPersonalizadoAsync(string banco);
    Task MigrarFinanciamientosAHogarAsync(string hogarId);
}
