using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface IReportCalculationService
{
    YearlySummary CalculateYearlySummary(List<Gasto> gastos);
    List<MonthlyTrend> CalculateMonthlyTrend(List<Gasto> gastos, int year);
    List<CategoryBreakdownItem> CalculateCategoryBreakdown(List<Gasto> gastos, Dictionary<Guid, Categoria> cacheCategorias);
    YearComparison CalculateYearComparison(List<Gasto> currentYearGastos, List<Gasto> previousYearGastos, List<CategoryBreakdownItem> currentBreakdown);
}

public class YearlySummary
{
    public decimal TotalGastos { get; set; }
    public decimal TotalIngresos { get; set; }
    public int MesesConDatos { get; set; }
    public decimal PromedioMensual { get; set; }
}

public class MonthlyTrend
{
    public int Mes { get; set; }
    public string Label { get; set; } = "";
    public decimal Gastos { get; set; }
    public decimal Ingresos { get; set; }
}

public class CategoryBreakdownItem
{
    public Guid CategoriaId { get; set; }
    public string Nombre { get; set; } = "";
    public string Icono { get; set; } = "";
    public string Color { get; set; } = "";
    public decimal Total { get; set; }
    public int Porcentaje { get; set; }
    public decimal TotalAnterior { get; set; }
    public decimal PorcentajeVariacion { get; set; }
}

public class YearComparison
{
    public decimal DeltaGastos { get; set; }
    public decimal DeltaGastosPct { get; set; }
    public decimal DeltaIngresos { get; set; }
    public decimal DeltaIngresosPct { get; set; }
    public decimal DeltaBalance { get; set; }
    public decimal DeltaBalancePct { get; set; }
    public decimal DeltaPromedio { get; set; }
    public decimal DeltaPromedioPct { get; set; }
    public int AnioAnterior { get; set; }
    public decimal GastosAnteriores { get; set; }
    public decimal IngresosAnteriores { get; set; }
}
