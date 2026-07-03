using System.Text.Json.Serialization;

namespace ControlDeGastos.Models;

public class ProgresoRPG
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public int Nivel { get; set; } = 1;
    public int ExpActual { get; set; } = 0;
    public int ExpRequerida { get; set; } = 100;
    public int HpActual { get; set; } = 100;
    public int HpMaximo { get; set; } = 100;
    public DateTime? UltimoGastoFecha { get; set; }
    public int GastosConsecutivos { get; set; } = 0;
    public int GastosEstePeriodo { get; set; } = 0;
    public int UltimoResetGastosMes { get; set; }
    public int UltimoResetGastosAnio { get; set; }
    private List<Guid> _logrosDesbloqueados = new();
    public List<Guid> LogrosDesbloqueados
    {
        get => _logrosDesbloqueados ??= new();
        set => _logrosDesbloqueados = value ?? new();
    }

    private HashSet<string> _idsCategoriasUsadas = new();
    public HashSet<string> IdsCategoriasUsadas
    {
        get => _idsCategoriasUsadas ??= new();
        set => _idsCategoriasUsadas = value ?? new();
    }

    public string? TituloActivoId { get; set; }

    private List<string> _titulosDesbloqueados = new();
    public List<string> TitulosDesbloqueados
    {
        get => _titulosDesbloqueados ??= new();
        set => _titulosDesbloqueados = value ?? new();
    }
    public int Monedas { get; set; } = 0;
    public int MonedasGastadas { get; set; } = 0;

    public double BoostExpMultiplicador { get; set; } = 1.0;
    public DateTime? BoostExpExpiracion { get; set; }
    public string? BoostExpItemId { get; set; }
    public int HpEscudoRestante { get; set; } = 0;
    public int EscudosRacha { get; set; } = 0;
    public bool PresupuestoExcedidoEsteMes { get; set; }
    public int MesesPresupuestoRespetado { get; set; }
    public int UltimoMesVerificadoPresupuesto { get; set; }
    public int UltimoAnioVerificadoPresupuesto { get; set; }

    public string? SkinTarjetaActiva { get; set; }

    private List<string> _idsSkinsCompradas = new();
    public List<string> IdsSkinsCompradas
    {
        get => _idsSkinsCompradas ??= new();
        set => _idsSkinsCompradas = value ?? new();
    }

    private List<string> _idsTitulosTienda = new();
    public List<string> IdsTitulosTienda
    {
        get => _idsTitulosTienda ??= new();
        set => _idsTitulosTienda = value ?? new();
    }

    private List<string> _idsExpansionesCompradas = new();
    public List<string> IdsExpansionesCompradas
    {
        get => _idsExpansionesCompradas ??= new();
        set => _idsExpansionesCompradas = value ?? new();
    }

    public int SchemaVersion { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}
