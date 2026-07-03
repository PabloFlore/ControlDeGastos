namespace ControlDeGastos.Models;

public enum TipoCondicionLogro
{
    GastosTotales,
    GastosConsecutivos,
    MontoTotalGastado,
    NivelAlcanzado,
    IngresosRegistrados,
    CategoriasUsadas,
    GastosCompartidos,
    RecurrenciasActivas,
    PresupuestosCreados,
    PresupuestosCumplidos,
}

public class Logro
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Icono { get; set; } = "🏅";
    public TipoCondicionLogro TipoCondicion { get; set; }
    public int ValorCondicion { get; set; }
    public int RecompensaExp { get; set; }
    public int RecompensaMonedas { get; set; }
    public int Orden { get; set; }
}
