using ControlDeGastos.Models;

namespace ControlDeGastos.Services.DataMigration.Migrations;

public class V3NumeroVersionEntidadesMigration : IDataMigration
{
    public int Version => 3;
    public string Descripcion => "Agrega NumeroVersion=1 a Presupuestos, Recurrencias, Financiamientos, Categorias y Usuario existentes sin versión";

    public async Task<bool> MigrateAsync(IStorageService storage)
    {
        var modificados = false;

        modificados |= await MigrarListaAsync<Presupuesto>(storage, "cdg_presupuestos");
        modificados |= await MigrarListaAsync<Recurrencia>(storage, "cdg_recurrencias");
        modificados |= await MigrarListaAsync<Financiamiento>(storage, "cdg_financiamientos");
        modificados |= await MigrarListaAsync<Categoria>(storage, "cdg_categorias");

        var usuario = await storage.GetAsync<Usuario>("cdg_usuario");
        if (usuario is not null && usuario.NumeroVersion == 0)
        {
            usuario.NumeroVersion = 1;
            await storage.SetAsync("cdg_usuario", usuario);
            modificados = true;
        }

        return modificados;
    }

    private static async Task<bool> MigrarListaAsync<T>(IStorageService storage, string key) where T : class
    {
        var items = await storage.GetAsync<List<T>>(key);
        if (items is null || items.Count == 0)
            return false;

        var propiedad = typeof(T).GetProperty("NumeroVersion");
        if (propiedad is null)
            return false;

        var modificados = false;
        foreach (var item in items)
        {
            var valor = (int)(propiedad.GetValue(item) ?? 0);
            if (valor == 0)
            {
                propiedad.SetValue(item, 1);
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
