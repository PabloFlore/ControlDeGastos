using System.Text.Json.Serialization;

namespace ControlDeGastos.Models;

public class Usuario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? TokenLicencia { get; set; }
    public TipoLicencia? TipoLicencia { get; set; }
    public PlanType PlanActivo { get; set; } = PlanType.Local;
    public bool ModoGamificadoActivo { get; set; } = false;
    public bool ExcluirRecurrentesDePresupuesto { get; set; } = false;
    public bool ExcluirCreditosDePresupuesto { get; set; } = false;
    public bool MostrarMinutos { get; set; } = false;
    public bool MostrarGraficaIngresos { get; set; } = false;
    public string? HogarId { get; set; }
    public string? HogarCodigo { get; set; }
    public string? SupabaseUserId { get; set; }
    public string Moneda { get; set; } = "MXN";
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public DateTime? FechaExpiracionLicencia { get; set; }
    public string? DispositivoFingerprint { get; set; }
    public int PinDelaySegundos { get; set; } = 30;
    [JsonIgnore]
    public int NumeroVersion { get; set; } = 1;
    public int SchemaVersion { get; set; }
    public bool Sincronizado { get; set; } = false;
    public DateTime? UpdatedAt { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
