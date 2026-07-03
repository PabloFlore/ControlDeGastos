using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public interface IExportImportService
{
    Task<byte[]> ExportarDatosAsync();
    Task<ResultadoImportacion> ImportarDatosAsync(byte[] archivo);
}
