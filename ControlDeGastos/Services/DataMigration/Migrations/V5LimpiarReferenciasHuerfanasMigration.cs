using ControlDeGastos.Models;

namespace ControlDeGastos.Services.DataMigration.Migrations;

public class V5LimpiarReferenciasHuerfanasMigration : IDataMigration
{
    public int Version => 5;
    public string Descripcion => "Elimina gastos y presupuestos con categorias inexistentes; limpia RecurrenciaId/FinanciamientoId huerfanos";

    public async Task<bool> MigrateAsync(IStorageService storage)
    {
        var modificados = false;

        var categoriasIds = (await storage.GetAsync<List<Categoria>>("cdg_categorias"))
            ?.Select(c => c.Id)
            .ToHashSet() ?? new HashSet<Guid>();

        var recurrenciasIds = (await storage.GetAsync<List<Recurrencia>>("cdg_recurrencias"))
            ?.Select(r => r.Id)
            .ToHashSet() ?? new HashSet<Guid>();

        var financiamientosIds = (await storage.GetAsync<List<Financiamiento>>("cdg_financiamientos"))
            ?.Select(f => f.Id)
            .ToHashSet() ?? new HashSet<Guid>();

        modificados |= await LimpiarGastosAsync(storage, categoriasIds, recurrenciasIds, financiamientosIds);
        modificados |= await LimpiarPresupuestosAsync(storage, categoriasIds);

        return modificados;
    }

    private static async Task<bool> LimpiarGastosAsync(
        IStorageService storage,
        HashSet<Guid> categoriasIds,
        HashSet<Guid> recurrenciasIds,
        HashSet<Guid> financiamientosIds)
    {
        var gastos = await storage.GetAsync<List<Gasto>>("cdg_gastos");
        if (gastos is null || gastos.Count == 0)
            return false;

        var modificados = false;

        var idsEliminar = gastos
            .Where(g => !categoriasIds.Contains(g.CategoriaId))
            .Select(g => g.Id)
            .ToHashSet();

        if (idsEliminar.Count > 0)
        {
            gastos.RemoveAll(g => idsEliminar.Contains(g.Id));
            modificados = true;
        }

        foreach (var gasto in gastos)
        {
            if (gasto.RecurrenciaId.HasValue && !recurrenciasIds.Contains(gasto.RecurrenciaId.Value))
            {
                gasto.RecurrenciaId = null;
                modificados = true;
            }

            if (gasto.FinanciamientoId.HasValue && !financiamientosIds.Contains(gasto.FinanciamientoId.Value))
            {
                gasto.FinanciamientoId = null;
                modificados = true;
            }
        }

        if (modificados)
        {
            await storage.SetAsync("cdg_gastos", gastos);
        }

        return modificados;
    }

    private static async Task<bool> LimpiarPresupuestosAsync(
        IStorageService storage,
        HashSet<Guid> categoriasIds)
    {
        var presupuestos = await storage.GetAsync<List<Presupuesto>>("cdg_presupuestos");
        if (presupuestos is null || presupuestos.Count == 0)
            return false;

        var idsEliminar = presupuestos
            .Where(p => p.CategoriaId.HasValue && !categoriasIds.Contains(p.CategoriaId.Value))
            .Select(p => p.Id)
            .ToHashSet();

        if (idsEliminar.Count == 0)
            return false;

        presupuestos.RemoveAll(p => idsEliminar.Contains(p.Id));
        await storage.SetAsync("cdg_presupuestos", presupuestos);
        return true;
    }
}
