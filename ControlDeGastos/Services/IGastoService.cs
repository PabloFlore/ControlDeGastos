using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface IGastoService
{
    Task<List<Gasto>> ObtenerGastosAsync();
    Task<List<Gasto>> ObtenerGastosPorMesAsync(int year, int month);
    Task<List<Gasto>> ObtenerGastosPorRangoAsync(DateTime desde, DateTime hasta);
    Task<PaginatedResult<Gasto>> ObtenerGastosPaginadoAsync(Paginacion paginacion, FiltroGasto? filtro = null);
    Task<Gasto> CrearGastoAsync(Gasto gasto);
    Task<Gasto> ActualizarGastoAsync(Gasto gasto);
    Task EliminarGastoAsync(Guid id);
    Task MarcarTodosPendientesSyncAsync();
    Task MigrarGastosAHogarAsync(string hogarId);
}
