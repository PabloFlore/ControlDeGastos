using System.Text.Json.Serialization;

namespace ControlDeGastos.Models;

public enum TipoRecurrencia
{
    Diario,
    Semanal,
    Mensual,
    Anual,
}

public class Recurrencia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public Guid? CategoriaId { get; set; }
    public decimal Monto { get; set; }
    public string? Descripcion { get; set; }
    public TipoRecurrencia TipoRecurrencia { get; set; } = TipoRecurrencia.Mensual;
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime? FechaFin { get; set; }
    public DateTime ProximaFecha { get; set; }
    public bool Activa { get; set; } = true;
    public int Intervalo { get; set; } = 1;
    public Guid? SubscriptionId { get; set; }
    public string? HogarId { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
    public DateTime ActualizadoEn { get; set; } = DateTime.UtcNow;
    public bool Sincronizado { get; set; }
    public DateTime? UpdatedAt { get; set; }
    [JsonIgnore]
    public int NumeroVersion { get; set; } = 1;
    public int SchemaVersion { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
