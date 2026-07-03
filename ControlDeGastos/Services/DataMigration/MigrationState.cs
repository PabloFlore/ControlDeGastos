namespace ControlDeGastos.Services.DataMigration;

public class MigrationState
{
    public int VersionActual { get; set; }
    public int VersionEsperada { get; set; }
    public List<MigrationInfo> MigracionesDisponibles { get; set; } = new();
    public List<MigrationInfo> MigracionesPendientes { get; set; } = new();

    public bool AlDia => VersionActual >= VersionEsperada;
}

public class MigrationInfo
{
    public int Version { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public bool Pendiente { get; set; }
}
