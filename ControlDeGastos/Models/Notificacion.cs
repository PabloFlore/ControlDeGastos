namespace ControlDeGastos.Models;

public class Notificacion
{
    public Guid Id { get; set; }
    public string Tipo { get; set; } = "";
    public string Mensaje { get; set; } = "";
    public string Icono { get; set; } = "";
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public Guid? ReferenciaId { get; set; }
}
