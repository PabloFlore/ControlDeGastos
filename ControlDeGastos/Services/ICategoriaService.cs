using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface ICategoriaService
{
    Task<List<Categoria>> ObtenerCategoriasAsync();
    Task<Categoria> CrearCategoriaAsync(Categoria categoria);
    Task<Categoria> ActualizarCategoriaAsync(Categoria categoria);
    Task EliminarCategoriaAsync(Guid id);
    Task InicializarCategoriasPorDefectoAsync();
    Task MigrarCategoriasAHogarAsync(string hogarId);
}
