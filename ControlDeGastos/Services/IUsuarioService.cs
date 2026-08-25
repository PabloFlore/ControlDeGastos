using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface IUsuarioService
{
    Task<Usuario> ObtenerUsuarioAsync();
    Task GuardarUsuarioAsync(Usuario usuario);
    Task SincronizarPerfilConNubeAsync();
    Task<PerfilRecord?> ObtenerPerfilRemotoAsync();
    Task CambiarPlanAsync(PlanType plan);
    Task CambiarModoGamificadoAsync(bool activo);
    Task CambiarExcluirRecurrentesAsync(bool excluir);
    Task CambiarExcluirCreditosAsync(bool excluir);
    Task CambiarMostrarMinutosAsync(bool mostrar);
    Task CambiarMostrarGraficaIngresosAsync(bool mostrar);
    Task CambiarMostrarGraficaPresupuestosAsync(bool mostrar);
}
