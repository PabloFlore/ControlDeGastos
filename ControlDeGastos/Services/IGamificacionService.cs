using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface IGamificacionService
{
    Task<ProgresoRPG> ObtenerProgresoAsync();
    Task AplicarGastoAsync(Gasto gasto, decimal gastadoPeriodo, decimal limitePeriodo);
    Task<ProgresoRPG> RecuperarHpDiarioAsync();
    Task<List<Logro>> ObtenerLogrosAsync();
    Task<List<Logro>> ObtenerLogrosDesbloqueadosAsync();
    Task<List<Logro>> VerificarYDesbloquearLogrosAsync();
    Task<(int actual, int requerido)> CalcularProgresoLogroAsync(Logro logro);
    Task RecalcularDesdeCeroAsync();
    Task<List<TituloCosmetico>> ObtenerTitulosAsync();
    Task<List<TituloCosmetico>> ObtenerTitulosDesbloqueadosAsync();
    Task<string?> ObtenerTituloActivoNombreAsync();
    Task<bool> EstablecerTituloActivoAsync(string? tituloId);
    Task VerificarYDesbloquearTitulosAsync();
    Task<(double multiplicador, DateTime? expiracion)> ObtenerBoostExpActivoAsync();
}
