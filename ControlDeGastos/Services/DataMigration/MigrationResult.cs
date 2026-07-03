namespace ControlDeGastos.Services.DataMigration;

public class MigrationResult
{
    public List<int> Ejecutadas { get; set; } = new();
    public List<int> ConCambios { get; set; } = new();
    public int? FallidaVersion { get; set; }
    public string? Error { get; set; }
    public bool Exito => Error is null;

    public override string ToString()
    {
        if (Error is not null)
            return $"Migración V{FallidaVersion} fallida: {Error}";

        if (Ejecutadas.Count == 0)
            return "No hay migraciones pendientes.";

        var partes = Ejecutadas.Select(v =>
        {
            var cambio = ConCambios.Contains(v) ? " (con cambios)" : " (sin cambios)";
            return $"V{v}{cambio}";
        });
        return $"Migraciones ejecutadas: {string.Join(", ", partes)}";
    }
}
