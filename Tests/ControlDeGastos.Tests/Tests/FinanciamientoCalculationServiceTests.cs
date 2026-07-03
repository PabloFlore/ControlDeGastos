using ControlDeGastos.Models;
using ControlDeGastos.Services;

namespace ControlDeGastos.Tests.Tests;

public class FinanciamientoCalculationServiceTests
{
    private readonly FinanciamientoCalculationService _service = new();
    private readonly Guid _catId = Guid.NewGuid();

    private Financiamiento MakeItem(decimal montoTotal, int plazoMeses, decimal? tasa = null, bool activo = true, DateTime? inicio = null)
        => new()
        {
            MontoTotal = montoTotal,
            PlazoMeses = plazoMeses,
            TasaInteresAnual = tasa,
            Activo = activo,
            FechaInicio = inicio ?? DateTime.UtcNow.AddMonths(-3),
            CategoriaId = _catId,
            Tipo = "Credito",
            Banco = "BBVA",
            Alias = "Test",
        };

    [Fact]
    public void CalculateMonthlyPayment_SinInteres_DivideMontoTotal()
    {
        var item = MakeItem(120000, 12);
        var result = _service.CalculateMonthlyPayment(item);
        Assert.Equal(10000m, result);
    }

    [Fact]
    public void CalculateMonthlyPayment_ConInteres_CalculaMontosPeriódicos()
    {
        var item = MakeItem(10000, 12, 12m);
        var result = _service.CalculateMonthlyPayment(item);
        Assert.True(result > 833m);
        Assert.True(result < 1000m);
    }

    [Fact]
    public void CalculateMonthlyPayment_PlazoCero_RetornaMontoTotal()
    {
        var item = MakeItem(5000, 0);
        var result = _service.CalculateMonthlyPayment(item);
        Assert.Equal(5000m, result);
    }

    [Fact]
    public void CalculateMonthlyPayment_PlazoUno_RetornaMontoTotal()
    {
        var item = MakeItem(5000, 1);
        var result = _service.CalculateMonthlyPayment(item);
        Assert.Equal(5000m, result);
    }

    [Fact]
    public void CalculateMonthlyPayment_TasaCero_Divide()
    {
        var item = MakeItem(6000, 6, 0m);
        var result = _service.CalculateMonthlyPayment(item);
        Assert.Equal(1000m, result);
    }

    [Fact]
    public void CalculateMonthlyPayment_MontoCero_RetornaCero()
    {
        var item = MakeItem(0, 12, 12m);
        var result = _service.CalculateMonthlyPayment(item);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateProgress_ItemActivo_CalculaProgreso()
    {
        var item = MakeItem(12000, 12, inicio: DateTime.UtcNow.AddMonths(-3));
        var result = _service.CalculateProgress(item);

        Assert.True(result.Completados >= 3);
        Assert.True(result.Porcentaje > 0);
        Assert.NotEqual("—", result.ProximoPago);
        Assert.True(result.Restante > 0);
        Assert.NotEqual("—", result.Vence);
    }

    [Fact]
    public void CalculateProgress_ItemInactivo_Retorna100Porciento()
    {
        var item = MakeItem(12000, 12, activo: false);
        var result = _service.CalculateProgress(item);

        Assert.Equal(12, result.Completados);
        Assert.Equal(100, result.Porcentaje);
        Assert.Equal("—", result.ProximoPago);
        Assert.Equal(0, result.Restante);
        Assert.Equal("—", result.Vence);
    }

    [Fact]
    public void CalculateProgress_RecienIniciado_ProximoPagoNoVacio()
    {
        var item = MakeItem(12000, 12, inicio: DateTime.UtcNow);
        var result = _service.CalculateProgress(item);

        Assert.Equal(1, result.Completados);
        Assert.NotEqual("—", result.ProximoPago);
    }

    [Fact]
    public void CalculateProgress_Vencido_RetornaVencido()
    {
        var item = MakeItem(12000, 1, inicio: DateTime.UtcNow.AddMonths(-2));
        var result = _service.CalculateProgress(item);

        Assert.Equal("Vencido", result.Vence);
    }

    [Fact]
    public void CalculateTotalDebt_SumaActivos()
    {
        var items = new List<Financiamiento>
        {
            MakeItem(5000, 12, activo: true),
            MakeItem(3000, 6, activo: true),
            MakeItem(2000, 12, activo: false),
        };

        var result = _service.CalculateTotalDebt(items);
        Assert.Equal(8000m, result);
    }

    [Fact]
    public void CalculateTotalDebt_SoloInactivos_RetornaCero()
    {
        var items = new List<Financiamiento>
        {
            MakeItem(5000, 12, activo: false),
        };

        var result = _service.CalculateTotalDebt(items);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateTotalMonthlyPayment_SumaPagosActivos()
    {
        var items = new List<Financiamiento>
        {
            MakeItem(120000, 12, activo: true),
            MakeItem(60000, 12, activo: true),
        };

        var result = _service.CalculateTotalMonthlyPayment(items);
        Assert.Equal(15000m, result);
    }

    [Fact]
    public void CalculateTotalMonthlyPayment_ExcluyeInactivos()
    {
        var items = new List<Financiamiento>
        {
            MakeItem(120000, 12, activo: true),
            MakeItem(60000, 12, activo: false),
        };

        var result = _service.CalculateTotalMonthlyPayment(items);
        Assert.Equal(10000m, result);
    }

    [Fact]
    public void AutoDeactivateExpired_ItemVencido_Desactiva()
    {
        var items = new List<Financiamiento>
        {
            MakeItem(1000, 2, inicio: DateTime.UtcNow.AddMonths(-3)),
        };

        _service.AutoDeactivateExpired(items);

        Assert.False(items[0].Activo);
    }

    [Fact]
    public void AutoDeactivateExpired_ItemVigente_MantieneActivo()
    {
        var items = new List<Financiamiento>
        {
            MakeItem(1000, 12, inicio: DateTime.UtcNow.AddMonths(-1)),
        };

        _service.AutoDeactivateExpired(items);

        Assert.True(items[0].Activo);
    }

    [Fact]
    public void AutoDeactivateExpired_Mixto_DesactivaSoloVencidos()
    {
        var items = new List<Financiamiento>
        {
            MakeItem(1000, 1, inicio: DateTime.UtcNow.AddMonths(-2)),
            MakeItem(1000, 12, inicio: DateTime.UtcNow.AddMonths(-1)),
        };

        _service.AutoDeactivateExpired(items);

        Assert.False(items[0].Activo);
        Assert.True(items[1].Activo);
    }
}
