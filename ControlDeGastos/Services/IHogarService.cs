using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface IHogarService
{
    Task<Hogar?> ObtenerHogarAsync(string hogarId);
    Task<Hogar?> ObtenerHogarPorCodigoAsync(string codigo);
    Task<List<HogarMiembro>> ObtenerMiembrosAsync(string hogarId);
    Task<Hogar> CrearHogarAsync();
    Task<bool> UnirseAHogarAsync(string codigo, string email);
    Task<bool> SalirDelHogarAsync(string hogarId, string email);
    Task GuardarLicenciaHogarAsync(string hogarId, Licencia licencia);
    Task<Licencia?> ObtenerLicenciaHogarAsync(string hogarId);
}
