using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface ITiendaService
{
    Task<List<ArticuloTienda>> ObtenerCatalogoAsync();
    Task<(bool exito, string mensaje)> ComprarItemAsync(string itemId);
    Task<bool> EquiparSkinAsync(string? skinId);
    Task<bool> EquiparTituloTiendaAsync(string tituloId);
    Task<string?> ObtenerSkinActivaAsync();
    Task<List<string>> ObtenerSkinsCompradasAsync();
    Task<List<string>> ObtenerTitulosTiendaCompradosAsync();
    Task<(double multiplicador, DateTime? expiracion, string? itemId)> ObtenerBoostExpActivoAsync();
    Task<int> ObtenerEscudoHpRestanteAsync();
    Task<int> ObtenerEscudosRachaAsync();
    Task<(bool exito, string mensaje, ArticuloTienda? itemGanado, int compensacion)> ProcesarCajaSorpresaAsync(string itemId);
}
