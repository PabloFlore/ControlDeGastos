using System.Text.Json.Serialization;

namespace ControlDeGastos.Models;

public class Financiamiento
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string Tipo { get; set; } = "Credito";
    public string Banco { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public decimal MontoTotal { get; set; }
    public int PlazoMeses { get; set; }
    public decimal? TasaInteresAnual { get; set; }
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime ProximaCuota { get; set; }
    public bool Activo { get; set; } = true;
    public Guid? CategoriaId { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoEn { get; set; }
    public string? HogarId { get; set; }
    public bool Sincronizado { get; set; }
    public DateTime? UpdatedAt { get; set; }
    [JsonIgnore]
    public int NumeroVersion { get; set; } = 1;
    public int SchemaVersion { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
