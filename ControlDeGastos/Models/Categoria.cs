using System.Text.Json.Serialization;

namespace ControlDeGastos.Models;

public class Categoria
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UsuarioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Icono { get; set; } = "📁";
    public string Color { get; set; } = "#6c757d";
    public TipoGasto Tipo { get; set; } = TipoGasto.Gasto;
    public int Orden { get; set; } = 0;
    public decimal? PresupuestoPorDefecto { get; set; }
    public bool EsPersonalizada { get; set; } = false;
    public string? HogarId { get; set; }
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    [JsonIgnore]
    public int NumeroVersion { get; set; } = 1;
    public int SchemaVersion { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
