namespace ControlDeGastos.Models;

public class HogarMiembro
{
    public string HogarId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string? Color { get; set; }
    public Guid? UsuarioId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
