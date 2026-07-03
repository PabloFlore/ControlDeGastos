namespace ControlDeGastos.Models;

public class Hogar
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CodigoInvitacion { get; set; } = string.Empty;
    public string CreadoPorEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? TokenHash { get; set; }
    public TipoLicencia LicenciaTipo { get; set; } = TipoLicencia.Trial;
    public DateTime? FechaExpiracion { get; set; }
    public bool ModoGamificadoIncluido { get; set; } = false;
    public PlanType PlanIncluido { get; set; } = PlanType.Nube;
}
