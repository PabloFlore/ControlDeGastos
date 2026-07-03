namespace ControlDeGastos.Models;

public enum CategoriaArticulo
{
    BoostExp,
    CuracionHp,
    ExpansionHpMax,
    TituloTienda,
    SkinTarjeta,
    EscudoRacha,
    CajaSorpresa
}

public enum TipoDuracionBoost
{
    Horas,
    Dias,
    Usos,
    Permanente
}

public class ArticuloTienda
{
    public string Id { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Icono { get; set; } = "📦";
    public CategoriaArticulo Categoria { get; set; }
    public int Precio { get; set; }
    public bool EsConsumible { get; set; }
    public bool EsBoost { get; set; }
    public TipoDuracionBoost TipoDuracion { get; set; }
    public int DuracionValor { get; set; }
    public double ValorNumerico { get; set; }
    public string? SkinCssClass { get; set; }
    public string? TituloId { get; set; }
    public string? TituloIcono { get; set; }
    public int Orden { get; set; }
}
