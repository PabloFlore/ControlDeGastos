using System.Text.Json;
using ControlDeGastos.Models;
using ControlDeGastos.Services.DataMigration;

namespace ControlDeGastos.Services;

public class ExportImportService : IExportImportService
{
    private readonly IStorageService _storage;
    private readonly IDataMigrationRunner _migrationRunner;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static int VersionActualExport => DataMigrationRunner.VersionActual;

    public ExportImportService(IStorageService storage, IDataMigrationRunner migrationRunner)
    {
        _storage = storage;
        _migrationRunner = migrationRunner;
    }

    public async Task<byte[]> ExportarDatosAsync()
    {
        var datos = new DatosExportacion
        {
            Version = VersionActualExport,
            SchemaVersion = VersionActualExport,
            ExportadoEn = DateTime.UtcNow,
            Datos = new DatosExportacionData
            {
                Usuario = await _storage.GetAsync<Usuario>("cdg_usuario"),
                Categorias = await _storage.GetAsync<List<Categoria>>("cdg_categorias") ?? new(),
                Gastos = await _storage.GetAsync<List<Gasto>>("cdg_gastos") ?? new(),
                Presupuestos = await _storage.GetAsync<List<Presupuesto>>("cdg_presupuestos") ?? new(),
                Recurrencias = await _storage.GetAsync<List<Recurrencia>>("cdg_recurrencias") ?? new(),
                Financiamientos = await _storage.GetAsync<List<Financiamiento>>("cdg_financiamientos") ?? new(),
                ProgresoRpg = await _storage.GetAsync<ProgresoRPG>("cdg_progreso_rpg"),
                BancosPersonalizados = await _storage.GetAsync<List<string>>("cdg_bancos_personalizados") ?? new(),
                UsedTokens = await _storage.GetAsync<List<string>>("cdg_used_tokens") ?? new(),
                NotificacionesVistasMap = await _storage.GetAsync<Dictionary<string, List<Guid>>>("cdg_notif_vistas_map")
            }
        };

        SellarSchemaVersion(datos.Datos);

        return JsonSerializer.SerializeToUtf8Bytes(datos, JsonOptions);
    }

    private static void SellarSchemaVersion(DatosExportacionData datos)
    {
        var versionActual = VersionActualExport;

        if (datos.Usuario is not null)
            datos.Usuario.SchemaVersion = versionActual;

        foreach (var item in datos.Categorias)
            item.SchemaVersion = versionActual;

        foreach (var item in datos.Gastos)
            item.SchemaVersion = versionActual;

        foreach (var item in datos.Presupuestos)
            item.SchemaVersion = versionActual;

        foreach (var item in datos.Recurrencias)
            item.SchemaVersion = versionActual;

        foreach (var item in datos.Financiamientos)
            item.SchemaVersion = versionActual;

        if (datos.ProgresoRpg is not null)
            datos.ProgresoRpg.SchemaVersion = versionActual;
    }

    public async Task<ResultadoImportacion> ImportarDatosAsync(byte[] archivo)
    {
        try
        {
            DatosExportacion? datos;

            try
            {
                datos = JsonSerializer.Deserialize<DatosExportacion>(archivo, JsonOptions);
            }
            catch (JsonException ex)
            {
                return new ResultadoImportacion
                {
                    Exito = false,
                    Mensaje = $"El archivo no tiene un formato JSON válido: {ex.Message}"
                };
            }

            if (datos is null)
            {
                return new ResultadoImportacion
                {
                    Exito = false,
                    Mensaje = "El archivo está vacío o no contiene datos válidos."
                };
            }

            if (datos.Version < 1 || datos.Version > VersionActualExport)
            {
                return new ResultadoImportacion
                {
                    Exito = false,
                    Mensaje = $"Versión de archivo no compatible ({datos.Version}). La versión actual es {VersionActualExport}."
                };
            }

            if (datos.Datos is null)
            {
                return new ResultadoImportacion
                {
                    Exito = false,
                    Mensaje = "El archivo no contiene la sección de datos requerida."
                };
            }

            await _storage.ClearAsync();

            if (datos.Datos.Usuario is not null)
                await _storage.SetAsync("cdg_usuario", datos.Datos.Usuario);

            await _storage.SetAsync("cdg_categorias", datos.Datos.Categorias);
            await _storage.SetAsync("cdg_gastos", datos.Datos.Gastos);
            await _storage.SetAsync("cdg_presupuestos", datos.Datos.Presupuestos);
            await _storage.SetAsync("cdg_recurrencias", datos.Datos.Recurrencias);
            await _storage.SetAsync("cdg_financiamientos", datos.Datos.Financiamientos);

            if (datos.Datos.ProgresoRpg is not null)
                await _storage.SetAsync("cdg_progreso_rpg", datos.Datos.ProgresoRpg);

            if (datos.Datos.BancosPersonalizados.Count > 0)
                await _storage.SetAsync("cdg_bancos_personalizados", datos.Datos.BancosPersonalizados);

            if (datos.Datos.UsedTokens.Count > 0)
                await _storage.SetAsync("cdg_used_tokens", datos.Datos.UsedTokens);

            if (datos.Datos.NotificacionesVistasMap is not null)
                await _storage.SetAsync("cdg_notif_vistas_map", datos.Datos.NotificacionesVistasMap);

            if (datos.Version < DataMigrationRunner.VersionActual)
            {
                await _storage.SetAsync("cdg_data_version", datos.Version);
                var migracionResult = await _migrationRunner.EjecutarMigrationsAsync();
                if (!migracionResult.Exito)
                {
                    return new ResultadoImportacion
                    {
                        Exito = false,
                        Mensaje = $"Los datos se importaron pero la migración falló: {migracionResult.Error}. Recarga la página para reintentar."
                    };
                }
            }

            var versionFinal = DataMigrationRunner.VersionActual;
            await SellarSchemaVersionStorageAsync(versionFinal);

            return new ResultadoImportacion
            {
                Exito = true,
                Mensaje = "Datos importados correctamente. La página se recargará.",
                TotalGastos = datos.Datos.Gastos.Count,
                TotalCategorias = datos.Datos.Categorias.Count,
                TotalPresupuestos = datos.Datos.Presupuestos.Count,
                TotalRecurrencias = datos.Datos.Recurrencias.Count,
                TotalFinanciamientos = datos.Datos.Financiamientos.Count
            };
        }
        catch (Exception ex)
        {
            return new ResultadoImportacion
            {
                Exito = false,
                Mensaje = $"Error inesperado al importar: {ex.Message}"
            };
        }
    }

    private async Task SellarSchemaVersionStorageAsync(int version)
    {
        var usuario = await _storage.GetAsync<Usuario>("cdg_usuario");
        if (usuario is not null)
        {
            usuario.SchemaVersion = version;
            await _storage.SetAsync("cdg_usuario", usuario);
        }

        var categorias = await _storage.GetAsync<List<Categoria>>("cdg_categorias");
        if (categorias is not null)
        {
            foreach (var item in categorias)
                item.SchemaVersion = version;
            await _storage.SetAsync("cdg_categorias", categorias);
        }

        var gastos = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        if (gastos is not null)
        {
            foreach (var item in gastos)
                item.SchemaVersion = version;
            await _storage.SetAsync("cdg_gastos", gastos);
        }

        var presupuestos = await _storage.GetAsync<List<Presupuesto>>("cdg_presupuestos");
        if (presupuestos is not null)
        {
            foreach (var item in presupuestos)
                item.SchemaVersion = version;
            await _storage.SetAsync("cdg_presupuestos", presupuestos);
        }

        var recurrencias = await _storage.GetAsync<List<Recurrencia>>("cdg_recurrencias");
        if (recurrencias is not null)
        {
            foreach (var item in recurrencias)
                item.SchemaVersion = version;
            await _storage.SetAsync("cdg_recurrencias", recurrencias);
        }

        var financiamientos = await _storage.GetAsync<List<Financiamiento>>("cdg_financiamientos");
        if (financiamientos is not null)
        {
            foreach (var item in financiamientos)
                item.SchemaVersion = version;
            await _storage.SetAsync("cdg_financiamientos", financiamientos);
        }

        var progreso = await _storage.GetAsync<ProgresoRPG>("cdg_progreso_rpg");
        if (progreso is not null)
        {
            progreso.SchemaVersion = version;
            await _storage.SetAsync("cdg_progreso_rpg", progreso);
        }
    }
}
