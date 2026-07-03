using ControlDeGastos.Models;
using ControlDeGastos.Services;

namespace ControlDeGastos.Tests.Tests;

public class ReportCalculationServiceTests
{
    private readonly ReportCalculationService _service = new();
    private readonly Dictionary<Guid, Categoria> _categorias;

    public ReportCalculationServiceTests()
    {
        _categorias = new Dictionary<Guid, Categoria>
        {
            [CatComidaId] = new() { Id = CatComidaId, Nombre = "Comida", Icono = "🍔", Color = "#ff0000" },
            [CatTransporteId] = new() { Id = CatTransporteId, Nombre = "Transporte", Icono = "🚗", Color = "#00ff00" },
            [CatSaludId] = new() { Id = CatSaludId, Nombre = "Salud", Icono = "🏥", Color = "#0000ff" },
        };
    }

    private static readonly Guid CatComidaId = Guid.NewGuid();
    private static readonly Guid CatTransporteId = Guid.NewGuid();
    private static readonly Guid CatSaludId = Guid.NewGuid();

    private static Gasto MakeGasto(decimal monto, Guid? categoriaId, int year, int month, int day)
        => new()
        {
            Monto = monto,
            CategoriaId = categoriaId ?? Guid.NewGuid(),
            Fecha = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Local),
        };

    [Fact]
    public void CalculateYearlySummary_VariosGastos_CalculaCorrectamente()
    {
        var gastos = new List<Gasto>
        {
            MakeGasto(500, CatComidaId, 2024, 1, 5),
            MakeGasto(300, CatTransporteId, 2024, 1, 10),
            MakeGasto(200, CatSaludId, 2024, 2, 15),
            MakeGasto(150, null, 2024, 3, 1),
            MakeGasto(-1000, CatComidaId, 2024, 1, 1),
        };

        var result = _service.CalculateYearlySummary(gastos);

        Assert.Equal(500 + 300 + 200 + 150, result.TotalGastos);
        Assert.Equal(1000, result.TotalIngresos);
        Assert.Equal(3, result.MesesConDatos);
    }

    [Fact]
    public void CalculateYearlySummary_GastosVacios_RetornaCero()
    {
        var result = _service.CalculateYearlySummary(new List<Gasto>());

        Assert.Equal(0, result.TotalGastos);
        Assert.Equal(0, result.TotalIngresos);
        Assert.Equal(1, result.MesesConDatos);
        Assert.Equal(0, result.PromedioMensual);
    }

    [Fact]
    public void CalculateYearlySummary_SoloGastos_IngresosCero()
    {
        var gastos = new List<Gasto>
        {
            MakeGasto(100, CatComidaId, 2024, 1, 1),
            MakeGasto(200, CatTransporteId, 2024, 1, 2),
        };

        var result = _service.CalculateYearlySummary(gastos);

        Assert.Equal(300, result.TotalGastos);
        Assert.Equal(0, result.TotalIngresos);
    }

    [Fact]
    public void CalculateYearlySummary_SoloIngresos_GastosCero()
    {
        var gastos = new List<Gasto>
        {
            MakeGasto(-500, CatComidaId, 2024, 1, 1),
            MakeGasto(-300, CatTransporteId, 2024, 1, 2),
        };

        var result = _service.CalculateYearlySummary(gastos);

        Assert.Equal(0, result.TotalGastos);
        Assert.Equal(800, result.TotalIngresos);
    }

    [Fact]
    public void CalculateMonthlyTrend_DoceMeses_GeneraTodos()
    {
        var gastos = new List<Gasto>();
        for (int m = 1; m <= 12; m++)
            gastos.Add(MakeGasto(100 * m, CatComidaId, 2024, m, 15));

        var result = _service.CalculateMonthlyTrend(gastos, 2024);

        Assert.Equal(12, result.Count);
        Assert.All(result, t => Assert.True(t.Gastos > 0));
        Assert.Equal("Ene", result[0].Label);
        Assert.Equal("Dic", result[11].Label);
    }

    [Fact]
    public void CalculateMonthlyTrend_SinGastos_TodosCero()
    {
        var result = _service.CalculateMonthlyTrend(new List<Gasto>(), 2024);

        Assert.Equal(12, result.Count);
        Assert.All(result, t => Assert.Equal(0, t.Gastos));
        Assert.All(result, t => Assert.Equal(0, t.Ingresos));
    }

    [Fact]
    public void CalculateMonthlyTrend_FiltraPorAnio()
    {
        var gastos = new List<Gasto>
        {
            MakeGasto(100, CatComidaId, 2023, 12, 1),
            MakeGasto(200, CatComidaId, 2024, 1, 1),
            MakeGasto(300, CatComidaId, 2025, 1, 1),
        };

        var result = _service.CalculateMonthlyTrend(gastos, 2024);

        Assert.Equal(200, result[0].Gastos);
        Assert.Equal(11, result.Count(t => t.Gastos == 0));
    }

    [Fact]
    public void CalculateCategoryBreakdown_AgrupaCorrectamente()
    {
        var gastos = new List<Gasto>
        {
            MakeGasto(100, CatComidaId, 2024, 1, 1),
            MakeGasto(200, CatComidaId, 2024, 1, 2),
            MakeGasto(300, CatTransporteId, 2024, 1, 3),
        };

        var result = _service.CalculateCategoryBreakdown(gastos, _categorias);

        Assert.Equal(2, result.Count);
        Assert.Equal("Comida", result[0].Nombre);
        Assert.Equal(300, result[0].Total);
        Assert.Equal("Transporte", result[1].Nombre);
        Assert.Equal(300, result[1].Total);
    }

    [Fact]
    public void CalculateCategoryBreakdown_SinCategoriaEnCache_UsaDefaults()
    {
        var catId = Guid.NewGuid();
        var gastos = new List<Gasto>
        {
            MakeGasto(100, catId, 2024, 1, 1),
        };

        var result = _service.CalculateCategoryBreakdown(gastos, _categorias);

        Assert.Single(result);
        Assert.Equal("Sin categoria", result[0].Nombre);
        Assert.Equal("📁", result[0].Icono);
        Assert.Equal("#6c757d", result[0].Color);
    }

    [Fact]
    public void CalculateCategoryBreakdown_CalculaPorcentajes()
    {
        var gastos = new List<Gasto>
        {
            MakeGasto(600, CatComidaId, 2024, 1, 1),
            MakeGasto(400, CatTransporteId, 2024, 1, 2),
        };

        var result = _service.CalculateCategoryBreakdown(gastos, _categorias);

        Assert.Equal(60, result[0].Porcentaje);
        Assert.Equal(40, result[1].Porcentaje);
    }

    [Fact]
    public void CalculateCategoryBreakdown_ExcluyeIngresos()
    {
        var gastos = new List<Gasto>
        {
            MakeGasto(100, CatComidaId, 2024, 1, 1),
            MakeGasto(-500, CatComidaId, 2024, 1, 2),
        };

        var result = _service.CalculateCategoryBreakdown(gastos, _categorias);

        Assert.Single(result);
        Assert.Equal(100, result[0].Total);
    }

    [Fact]
    public void CalculateCategoryBreakdown_IngresosSolamente_RetornaVacio()
    {
        var gastos = new List<Gasto>
        {
            MakeGasto(-500, CatComidaId, 2024, 1, 1),
        };

        var result = _service.CalculateCategoryBreakdown(gastos, _categorias);

        Assert.Empty(result);
    }

    [Fact]
    public void CalculateYearComparison_GastosAumentaron_DeltaPositivo()
    {
        var current = new List<Gasto>
        {
            MakeGasto(1000, CatComidaId, 2024, 1, 1),
        };
        var prev = new List<Gasto>
        {
            MakeGasto(500, CatComidaId, 2023, 1, 1),
        };

        var breakdown = _service.CalculateCategoryBreakdown(current, _categorias);
        var result = _service.CalculateYearComparison(current, prev, breakdown);

        Assert.Equal(500, result.DeltaGastos);
        Assert.Equal(100, result.DeltaGastosPct);
    }

    [Fact]
    public void CalculateYearComparison_GastosDisminuyeron_DeltaNegativo()
    {
        var current = new List<Gasto>
        {
            MakeGasto(300, CatComidaId, 2024, 1, 1),
        };
        var prev = new List<Gasto>
        {
            MakeGasto(900, CatComidaId, 2023, 1, 1),
        };

        var breakdown = _service.CalculateCategoryBreakdown(current, _categorias);
        var result = _service.CalculateYearComparison(current, prev, breakdown);

        Assert.Equal(-600, result.DeltaGastos);
    }

    [Fact]
    public void CalculateYearComparison_AnioAnteriorVacio_DeltaCero()
    {
        var current = new List<Gasto>
        {
            MakeGasto(500, CatComidaId, 2024, 1, 1),
        };

        var breakdown = _service.CalculateCategoryBreakdown(current, _categorias);
        var result = _service.CalculateYearComparison(current, new List<Gasto>(), breakdown);

        Assert.Equal(500, result.DeltaGastos);
        Assert.Equal(0, result.DeltaGastosPct);
    }

    [Fact]
    public void CalculateYearComparison_AsignaTotalesAnterioresPorCategoria()
    {
        var current = new List<Gasto>
        {
            MakeGasto(200, CatComidaId, 2024, 1, 1),
            MakeGasto(100, CatTransporteId, 2024, 1, 1),
        };
        var prev = new List<Gasto>
        {
            MakeGasto(100, CatComidaId, 2023, 1, 1),
            MakeGasto(50, CatTransporteId, 2023, 1, 1),
        };

        var breakdown = _service.CalculateCategoryBreakdown(current, _categorias);
        _service.CalculateYearComparison(current, prev, breakdown);

        var comida = breakdown.First(b => b.CategoriaId == CatComidaId);
        Assert.Equal(100, comida.TotalAnterior);
        Assert.Equal(100, comida.PorcentajeVariacion);

        var transporte = breakdown.First(b => b.CategoriaId == CatTransporteId);
        Assert.Equal(50, transporte.TotalAnterior);
        Assert.Equal(100, transporte.PorcentajeVariacion);
    }
}
