using ControlDeGastos.Models;
using ControlDeGastos.Services.DataMigration;

namespace ControlDeGastos.Services.DataMigration.Migrations;

public class V2NumeroVersionGastosMigration : IDataMigration
{
    public int Version => 2;
    public string Descripcion => "Agrega NumeroVersion=1 a gastos existentes sin versión";

    public async Task<bool> MigrateAsync(IStorageService storage)
    {
        var gastos = await storage.GetAsync<List<Gasto>>("cdg_gastos");
        if (gastos is null || gastos.Count == 0)
            return false;

        var modificados = false;
        foreach (var g in gastos)
        {
            if (g.NumeroVersion == 0)
            {
                g.NumeroVersion = 1;
                modificados = true;
            }
        }

        if (modificados)
        {
            await storage.SetAsync("cdg_gastos", gastos);
        }

        return modificados;
    }
}
