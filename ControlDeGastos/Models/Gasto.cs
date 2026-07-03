using System.Text.Json.Serialization;

namespace ControlDeGastos.Models;

public class Gasto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public Guid CategoriaId { get; set; }
    public decimal Monto { get; set; }
    public string? Descripcion { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? RecurrenciaId { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? FinanciamientoId { get; set; }
    public bool EsGastoCompartido { get; set; } = false;
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoEn { get; set; }
    public string? HogarId { get; set; }
    public bool Sincronizado { get; set; } = false;
    public DateTime? UpdatedAt { get; set; }
    [JsonIgnore]
    public int NumeroVersion { get; set; } = 1;
    public int SchemaVersion { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
