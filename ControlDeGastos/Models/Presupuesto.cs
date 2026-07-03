using System.Text.Json.Serialization;

namespace ControlDeGastos.Models;

public class Presupuesto
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public Guid? CategoriaId { get; set; }
    public decimal MontoLimite { get; set; }
    public PeriodoPresupuesto Periodo { get; set; } = PeriodoPresupuesto.Mensual;
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime? FechaFin { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    public string? HogarId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    [JsonIgnore]
    public int NumeroVersion { get; set; } = 1;
    public int SchemaVersion { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
