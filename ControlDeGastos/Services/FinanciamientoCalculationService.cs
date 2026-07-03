using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class FinanciamientoCalculationService : IFinanciamientoCalculationService
{
    public decimal CalculateMonthlyPayment(Financiamiento item)
    {
        if (item.PlazoMeses <= 0) return item.MontoTotal;
        if (item.TasaInteresAnual.HasValue && item.TasaInteresAnual > 0 && item.MontoTotal > 0)
        {
            var r = (item.TasaInteresAnual.Value / 100m) / 12m;
            var unoMasR = 1m + r;
            var potencia = (decimal)Math.Pow((double)unoMasR, item.PlazoMeses);
            if (potencia != 1)
                return item.MontoTotal * r * potencia / (potencia - 1m);
        }
        return item.MontoTotal / item.PlazoMeses;
    }

    public FinanciamientoProgress CalculateProgress(Financiamiento item, bool mostrarMinutos = false)
    {
        if (!item.Activo)
            return new FinanciamientoProgress
            {
                Completados = item.PlazoMeses,
                Porcentaje = 100,
                ProximoPago = "—",
                Restante = 0,
                Vence = "—",
            };

        var montoPago = CalculateMonthlyPayment(item);

        var completados = 0;
        var fechaProximo = item.FechaInicio;
        while (fechaProximo <= DateTime.UtcNow && completados < item.PlazoMeses)
        {
            fechaProximo = fechaProximo.AddMonths(1);
            completados++;
        }
        completados = Math.Max(1, completados);
        var pct = item.PlazoMeses > 0 ? (int)(completados * 100 / item.PlazoMeses) : 0;
        var restante = Math.Max(0, (item.PlazoMeses - completados) * montoPago);

        var fmtPago = mostrarMinutos ? "dd/MMM HH:mm" : "dd/MMM";
        var proxStr = fechaProximo > DateTime.UtcNow
            ? fechaProximo.ToString(fmtPago, null)
            : "—";

        var fechaVence = item.FechaInicio.AddMonths(item.PlazoMeses);
        var fmtVence = mostrarMinutos ? "dd/MMM/yyyy HH:mm" : "dd/MMM/yyyy";
        var venceStr = fechaVence > DateTime.UtcNow
            ? fechaVence.ToString(fmtVence, null)
            : "Vencido";

        return new FinanciamientoProgress
        {
            Completados = completados,
            Porcentaje = pct,
            ProximoPago = proxStr,
            Restante = restante,
            Vence = venceStr,
        };
    }

    public decimal CalculateTotalDebt(List<Financiamiento> items)
    {
        return items.Where(i => i.Activo).Sum(i => i.MontoTotal);
    }

    public decimal CalculateTotalMonthlyPayment(List<Financiamiento> items)
    {
        return items.Where(i => i.Activo).Sum(i => CalculateMonthlyPayment(i));
    }

    public List<Financiamiento> AutoDeactivateExpired(List<Financiamiento> items)
    {
        var now = DateTime.UtcNow;
        foreach (var item in items.Where(i => i.Activo))
        {
            if (now >= item.FechaInicio.AddMonths(item.PlazoMeses))
            {
                item.Activo = false;
            }
        }
        return items;
    }
}
