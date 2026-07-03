namespace ControlDeGastos.Models;

public class PerfilRecord
{
    public Guid Id { get; set; }
    public string? Nombre { get; set; }
    public string? Moneda { get; set; }
    public bool? ModoGamificadoActivo { get; set; }
    public bool? ExcluirRecurrentesDePresupuesto { get; set; }
    public bool? ExcluirCreditosDePresupuesto { get; set; }
    public int? PinDelaySegundos { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
