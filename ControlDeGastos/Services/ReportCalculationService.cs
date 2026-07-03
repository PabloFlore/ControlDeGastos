using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class ReportCalculationService : IReportCalculationService
{
    private static readonly string[] NombresMes =
        ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio",
         "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];

    public YearlySummary CalculateYearlySummary(List<Gasto> gastos)
    {
        ArgumentNullException.ThrowIfNull(gastos);

        decimal totalGastos = 0, totalIngresos = 0;
        var mesesSet = new HashSet<(int Year, int Month)>();
        foreach (var g in gastos)
        {
            if (g.Monto > 0) totalGastos += g.Monto;
            else totalIngresos += Math.Abs(g.Monto);
            var local = g.Fecha.ToLocalTime();
            mesesSet.Add((local.Year, local.Month));
        }
        var mesesConDatos = Math.Max(1, mesesSet.Count);

        return new YearlySummary
        {
            TotalGastos = totalGastos,
            TotalIngresos = totalIngresos,
            MesesConDatos = mesesConDatos,
            PromedioMensual = totalGastos / mesesConDatos,
        };
    }

    public List<MonthlyTrend> CalculateMonthlyTrend(List<Gasto> gastos, int year)
    {
        ArgumentNullException.ThrowIfNull(gastos);

        var gastosPorMes = new Dictionary<int, (decimal Gastos, decimal Ingresos)>();
        foreach (var g in gastos)
        {
            var local = g.Fecha.ToLocalTime();
            if (local.Year != year) continue;
            var data = gastosPorMes.GetValueOrDefault(local.Month);
            if (g.Monto > 0)
                gastosPorMes[local.Month] = (data.Gastos + g.Monto, data.Ingresos);
            else
                gastosPorMes[local.Month] = (data.Gastos, data.Ingresos + Math.Abs(g.Monto));
        }

        return Enumerable.Range(1, 12).Select(mes =>
        {
            var data = gastosPorMes.GetValueOrDefault(mes);
            return new MonthlyTrend
            {
                Mes = mes,
                Label = NombresMes[mes - 1][..3],
                Gastos = data.Gastos,
                Ingresos = data.Ingresos,
            };
        }).ToList();
    }

    public List<CategoryBreakdownItem> CalculateCategoryBreakdown(List<Gasto> gastos, Dictionary<Guid, Categoria> cacheCategorias)
    {
        ArgumentNullException.ThrowIfNull(gastos);
        ArgumentNullException.ThrowIfNull(cacheCategorias);

        var agrupados = gastos
            .Where(g => g.Monto > 0)
            .GroupBy(g => g.CategoriaId)
            .Select(g =>
            {
                var cat = cacheCategorias.GetValueOrDefault(g.Key);
                var total = g.Sum(x => x.Monto);
                return new CategoryBreakdownItem
                {
                    CategoriaId = g.Key,
                    Nombre = cat?.Nombre ?? "Sin categoria",
                    Icono = cat?.Icono ?? "📁",
                    Color = cat?.Color ?? "#6c757d",
                    Total = total,
                };
            })
            .OrderByDescending(g => g.Total)
            .ToList();

        var granTotal = agrupados.Sum(g => g.Total);
        foreach (var item in agrupados)
        {
            item.Porcentaje = granTotal > 0 ? (int)Math.Round(item.Total * 100 / granTotal) : 0;
        }

        return agrupados;
    }

    public YearComparison CalculateYearComparison(
        List<Gasto> currentYearGastos,
        List<Gasto> previousYearGastos,
        List<CategoryBreakdownItem> currentBreakdown)
    {
        ArgumentNullException.ThrowIfNull(currentYearGastos);
        ArgumentNullException.ThrowIfNull(previousYearGastos);
        ArgumentNullException.ThrowIfNull(currentBreakdown);

        decimal gastosActuales = 0, ingresosActuales = 0;
        var mesesActualSet = new HashSet<int>();
        int? anioActual = null;
        foreach (var g in currentYearGastos)
        {
            var local = g.Fecha.ToLocalTime();
            anioActual = local.Year;
            mesesActualSet.Add(local.Month);
            if (g.Monto > 0) gastosActuales += g.Monto;
            else ingresosActuales += Math.Abs(g.Monto);
        }
        var balanceActual = ingresosActuales - gastosActuales;
        var mesesActuales = Math.Max(1, mesesActualSet.Count);
        var promActual = gastosActuales / mesesActuales;

        decimal gastosPrev = 0, ingresosPrev = 0;
        var mesesPrevSet = new HashSet<int>();
        var totalsPrev = new Dictionary<Guid, decimal>();
        foreach (var g in previousYearGastos)
        {
            var local = g.Fecha.ToLocalTime();
            mesesPrevSet.Add(local.Month);
            if (g.Monto > 0)
            {
                gastosPrev += g.Monto;
                totalsPrev[g.CategoriaId] = totalsPrev.GetValueOrDefault(g.CategoriaId) + g.Monto;
            }
            else
            {
                ingresosPrev += Math.Abs(g.Monto);
            }
        }
        var balancePrev = ingresosPrev - gastosPrev;
        var mesesPrev = Math.Max(1, mesesPrevSet.Count);
        var promPrev = gastosPrev / mesesPrev;

        var comparison = new YearComparison
        {
            DeltaGastos = gastosActuales - gastosPrev,
            DeltaGastosPct = gastosPrev > 0 ? (gastosActuales - gastosPrev) * 100 / gastosPrev : 0,
            DeltaIngresos = ingresosActuales - ingresosPrev,
            DeltaIngresosPct = ingresosPrev > 0 ? (ingresosActuales - ingresosPrev) * 100 / ingresosPrev : 0,
            DeltaBalance = balanceActual - balancePrev,
            DeltaBalancePct = balancePrev != 0 ? (balanceActual - balancePrev) * 100 / Math.Abs(balancePrev) : 0,
            DeltaPromedio = promActual - promPrev,
            DeltaPromedioPct = promPrev > 0 ? (promActual - promPrev) * 100 / promPrev : 0,
            AnioAnterior = (anioActual ?? DateTime.Now.Year) - 1,
            GastosAnteriores = gastosPrev,
            IngresosAnteriores = ingresosPrev,
        };

        foreach (var item in currentBreakdown)
        {
            item.TotalAnterior = totalsPrev.GetValueOrDefault(item.CategoriaId, 0);
            item.PorcentajeVariacion = item.TotalAnterior > 0
                ? (item.Total - item.TotalAnterior) * 100 / item.TotalAnterior
                : item.Total > 0 ? 100 : 0;
        }

        return comparison;
    }
}
