using System.Text.Json.Serialization;

namespace ControlDeGastos.Models;

public class DatosExportacion
{
    public int Version { get; set; } = 1;
    public int SchemaVersion { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
    public DateTime ExportadoEn { get; set; } = DateTime.UtcNow;
    public DatosExportacionData Datos { get; set; } = new();
}

public class DatosExportacionData
{
    public Usuario? Usuario { get; set; }
    public List<Categoria> Categorias { get; set; } = new();
    public List<Gasto> Gastos { get; set; } = new();
    public List<Presupuesto> Presupuestos { get; set; } = new();
    public List<Recurrencia> Recurrencias { get; set; } = new();
    public List<Financiamiento> Financiamientos { get; set; } = new();
    public ProgresoRPG? ProgresoRpg { get; set; }
    public List<string> BancosPersonalizados { get; set; } = new();
    public List<string> UsedTokens { get; set; } = new();
    public Dictionary<string, List<Guid>>? NotificacionesVistasMap { get; set; }
    public int SchemaVersion { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
