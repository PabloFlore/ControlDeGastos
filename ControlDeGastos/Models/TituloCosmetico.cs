namespace ControlDeGastos.Models;

public enum TipoCondicionTitulo
{
    LogroEspecifico,
    NivelMinimo,
    RachaMinima,
    LogrosTotales,
    MontoAhorrado,
    GastosCompartidos,
}

public class TituloCosmetico
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Icono { get; set; } = "🎖️";
    public TipoCondicionTitulo TipoCondicion { get; set; }
    public int ValorCondicion { get; set; }
    public Guid? LogroRequerido { get; set; }
    public int Orden { get; set; }
}
