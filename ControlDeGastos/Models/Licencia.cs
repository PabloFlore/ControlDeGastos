using System.Text.Json.Serialization;

namespace ControlDeGastos.Models;

public class Licencia
{
    public string Token { get; set; } = string.Empty;
    public string? TokenHash { get; set; }
    public TipoLicencia LicenciaTipo { get; set; }
    public DateTime? FechaExpiracion { get; set; }
    public DateTime FechaActivacion { get; set; } = DateTime.UtcNow;
    public string? DispositivoId { get; set; }
    public DateTime? UltimaValidacion { get; set; }
    public bool Valida { get; set; } = false;
    public string Mensaje { get; set; } = string.Empty;
    public PlanType PlanIncluido { get; set; } = PlanType.Local;
    public bool ModoGamificadoIncluido { get; set; } = false;
    public int SchemaVersion { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }

    public int DiasRestantes
    {
        get
        {
            if (LicenciaTipo == TipoLicencia.ParaSiempre)
                return -1;
            if (FechaExpiracion == null)
                return 0;
            var diff = FechaExpiracion.Value.Date - DateTime.UtcNow.Date;
            return diff.Days > 0 ? diff.Days : 0;
        }
    }

    public int HorasRestantes
    {
        get
        {
            if (LicenciaTipo == TipoLicencia.ParaSiempre)
                return -1;
            if (FechaExpiracion == null)
                return 0;
            var diff = FechaExpiracion.Value - DateTime.UtcNow;
            return diff.TotalHours > 0 ? (int)Math.Floor(diff.TotalHours) : 0;
        }
    }
}
