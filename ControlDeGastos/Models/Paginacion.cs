namespace ControlDeGastos.Models;

public class Paginacion
{
    public int Pagina { get; set; } = 1;
    public int TamanoPagina { get; set; } = 50;
    public int Skip => (Pagina - 1) * TamanoPagina;
}

public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Total { get; set; }
    public int Pagina { get; set; }
    public int TamanoPagina { get; set; }
    public int TotalPaginas => TamanoPagina > 0 ? (int)Math.Ceiling((double)Total / TamanoPagina) : 0;
    public bool TienePaginaAnterior => Pagina > 1;
    public bool TienePaginaSiguiente => Pagina < TotalPaginas;
}

public class FiltroGasto
{
    public int? Year { get; set; }
    public int? Month { get; set; }
    public DateTime? Desde { get; set; }
    public DateTime? Hasta { get; set; }
    public Guid? CategoriaId { get; set; }
    public string? TextoBusqueda { get; set; }
    public string? TipoGasto { get; set; }
}
