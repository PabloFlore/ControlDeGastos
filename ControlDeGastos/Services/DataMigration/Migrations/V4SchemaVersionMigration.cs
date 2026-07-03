using ControlDeGastos.Models;

namespace ControlDeGastos.Services.DataMigration.Migrations;

public class V4SchemaVersionMigration : IDataMigration
{
    public int Version => 4;
    public string Descripcion => "Inicializa SchemaVersion a 4 en todas las entidades existentes para tracking de versión de schema";

    public async Task<bool> MigrateAsync(IStorageService storage)
    {
        var modificados = false;

        modificados |= await MigrarEntidadAsync<Gasto>(storage, "cdg_gastos");
        modificados |= await MigrarEntidadAsync<Presupuesto>(storage, "cdg_presupuestos");
        modificados |= await MigrarEntidadAsync<Recurrencia>(storage, "cdg_recurrencias");
        modificados |= await MigrarEntidadAsync<Financiamiento>(storage, "cdg_financiamientos");
        modificados |= await MigrarEntidadAsync<Categoria>(storage, "cdg_categorias");

        var usuario = await storage.GetAsync<Usuario>("cdg_usuario");
        if (usuario is not null && usuario.SchemaVersion == 0)
        {
            usuario.SchemaVersion = 4;
            await storage.SetAsync("cdg_usuario", usuario);
            modificados = true;
        }

        var progresoRpg = await storage.GetAsync<ProgresoRPG>("cdg_progreso_rpg");
        if (progresoRpg is not null && progresoRpg.SchemaVersion == 0)
        {
            progresoRpg.SchemaVersion = 4;
            await storage.SetAsync("cdg_progreso_rpg", progresoRpg);
            modificados = true;
        }

        var licencia = await storage.GetAsync<Licencia>("cdg_licencia");
        if (licencia is not null && licencia.SchemaVersion == 0)
        {
            licencia.SchemaVersion = 4;
            await storage.SetAsync("cdg_licencia", licencia);
            modificados = true;
        }

        return modificados;
    }

    private static async Task<bool> MigrarEntidadAsync<T>(IStorageService storage, string key) where T : class
    {
        var items = await storage.GetAsync<List<T>>(key);
        if (items is null || items.Count == 0)
            return false;

        var propiedad = typeof(T).GetProperty("SchemaVersion");
        if (propiedad is null)
            return false;

        var modificados = false;
        foreach (var item in items)
        {
            var valor = (int)(propiedad.GetValue(item) ?? 0);
            if (valor == 0)
            {
                propiedad.SetValue(item, 4);
                modificados = true;
            }
        }

        if (modificados)
        {
            await storage.SetAsync(key, items);
        }

        return modificados;
    }
}
