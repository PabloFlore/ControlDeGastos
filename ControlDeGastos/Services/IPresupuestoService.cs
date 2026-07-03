using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface IPresupuestoService
{
    Task<List<Presupuesto>> ObtenerPresupuestosAsync();
    Task<Presupuesto> CrearPresupuestoAsync(Presupuesto presupuesto);
    Task EliminarPresupuestoAsync(Guid id);
    Task<decimal> ObtenerGastadoEnPeriodoAsync(Presupuesto presupuesto);
    Task<decimal> CalcularGastadoAsync(Presupuesto presupuesto, List<Gasto> gastos);
    Task<List<Gasto>> FiltrarGastosParaPresupuestoAsync(List<Gasto> gastos);
    Task ActualizarPresupuestoAsync(Presupuesto presupuesto);
    Task MigrarPresupuestosAHogarAsync(string hogarId);
}
