namespace ControlDeGastos.Models;

public class ResultadoImportacion
{
    public bool Exito { get; set; }
    public string? Mensaje { get; set; }
    public int TotalGastos { get; set; }
    public int TotalCategorias { get; set; }
    public int TotalPresupuestos { get; set; }
    public int TotalRecurrencias { get; set; }
    public int TotalFinanciamientos { get; set; }
}
