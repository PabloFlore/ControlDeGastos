using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class PendienteEliminarSync
{
    public string Tabla { get; set; } = "";
    public Guid Id { get; set; }
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
}

public class SyncState
{
    public DateTime? UltimaSync { get; set; }
    public int PendientesSubir { get; set; }
    public int PendientesBajar { get; set; }
    public bool Sincronizando { get; set; }
    public DateTime? SincronizandoDesde { get; set; }
    public string? MensajeError { get; set; }
    public string? UltimoCheckpoint { get; set; }
    public bool HaySyncPendiente { get; set; }
    public bool SyncSaltada { get; set; }
    public string? SaltadaRazon { get; set; }
    public List<PendienteEliminarSync> PendientesEliminar { get; set; } = new();
}

public interface ISyncService
{
    Task<SyncState> ObtenerEstadoSyncAsync();
    Task SincronizarAhoraAsync();
    Task RegistrarPendienteEliminarAsync(string tabla, Guid id);
}
