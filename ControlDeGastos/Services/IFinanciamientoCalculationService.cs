using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface IFinanciamientoCalculationService
{
    decimal CalculateMonthlyPayment(Financiamiento item);
    FinanciamientoProgress CalculateProgress(Financiamiento item, bool mostrarMinutos = false);
    decimal CalculateTotalDebt(List<Financiamiento> items);
    decimal CalculateTotalMonthlyPayment(List<Financiamiento> items);
    List<Financiamiento> AutoDeactivateExpired(List<Financiamiento> items);
}

public class FinanciamientoProgress
{
    public int Completados { get; set; }
    public int Porcentaje { get; set; }
    public string ProximoPago { get; set; } = "";
    public decimal Restante { get; set; }
    public string Vence { get; set; } = "";
}
