using ControlDeGastos.Models;

namespace ControlDeGastos.Services.DataMigration.Migrations;

public class V7NormalizarDatosMigration : IDataMigration
{
    public int Version => 7;
    public string Descripcion => "Normaliza datos: corrige IDs vacios, strings nulos, y elimina entidades corruptas";

    public async Task<bool> MigrateAsync(IStorageService storage)
    {
        var modificados = false;

        modificados |= await NormalizarGastosAsync(storage);
        modificados |= await NormalizarCategoriasAsync(storage);
        modificados |= await NormalizarPresupuestosAsync(storage);
        modificados |= await NormalizarRecurrenciasAsync(storage);
        modificados |= await NormalizarFinanciamientosAsync(storage);
        modificados |= await NormalizarProgresoRpgAsync(storage);
        modificados |= await NormalizarUsuarioAsync(storage);

        return modificados;
    }

    private static async Task<bool> NormalizarGastosAsync(IStorageService storage)
    {
        var gastos = await storage.GetAsync<List<Gasto>>("cdg_gastos");
        if (gastos is null || gastos.Count == 0)
            return false;

        var modificados = false;

        var corruptos = gastos.Where(g => g.Id == Guid.Empty).ToList();
        if (corruptos.Count > 0)
        {
            gastos.RemoveAll(g => corruptos.Contains(g));
            modificados = true;
        }

        foreach (var g in gastos)
        {
            if (g.Descripcion is null)
            {
                g.Descripcion = string.Empty;
                modificados = true;
            }

            if (g.Monto == 0 && g.Descripcion == string.Empty)
            {
                gastos.Remove(g);
                modificados = true;
            }
        }

        if (modificados)
            await storage.SetAsync("cdg_gastos", gastos);

        return modificados;
    }

    private static async Task<bool> NormalizarCategoriasAsync(IStorageService storage)
    {
        var categorias = await storage.GetAsync<List<Categoria>>("cdg_categorias");
        if (categorias is null || categorias.Count == 0)
            return false;

        var modificados = false;

        var corruptos = categorias.Where(c => c.Id == Guid.Empty).ToList();
        if (corruptos.Count > 0)
        {
            categorias.RemoveAll(c => corruptos.Contains(c));
            modificados = true;
        }

        foreach (var c in categorias)
        {
            if (c.Nombre is null || c.Nombre.Trim() == string.Empty)
            {
                c.Nombre = "Sin nombre";
                modificados = true;
            }

            if (c.Icono is null)
            {
                c.Icono = "📁";
                modificados = true;
            }

            if (c.Color is null)
            {
                c.Color = "#6c757d";
                modificados = true;
            }
        }

        if (modificados)
            await storage.SetAsync("cdg_categorias", categorias);

        return modificados;
    }

    private static async Task<bool> NormalizarPresupuestosAsync(IStorageService storage)
    {
        var presupuestos = await storage.GetAsync<List<Presupuesto>>("cdg_presupuestos");
        if (presupuestos is null || presupuestos.Count == 0)
            return false;

        var modificados = false;

        var corruptos = presupuestos.Where(p => p.Id == Guid.Empty).ToList();
        if (corruptos.Count > 0)
        {
            presupuestos.RemoveAll(p => corruptos.Contains(p));
            modificados = true;
        }

        foreach (var p in presupuestos)
        {
            if (p.MontoLimite <= 0)
            {
                p.MontoLimite = 1;
                modificados = true;
            }

            if (p.FechaFin.HasValue && p.FechaFin < p.FechaInicio)
            {
                p.FechaFin = p.FechaInicio.AddMonths(1);
                modificados = true;
            }
        }

        if (modificados)
            await storage.SetAsync("cdg_presupuestos", presupuestos);

        return modificados;
    }

    private static async Task<bool> NormalizarRecurrenciasAsync(IStorageService storage)
    {
        var recurrencias = await storage.GetAsync<List<Recurrencia>>("cdg_recurrencias");
        if (recurrencias is null || recurrencias.Count == 0)
            return false;

        var modificados = false;

        var corruptos = recurrencias.Where(r => r.Id == Guid.Empty).ToList();
        if (corruptos.Count > 0)
        {
            recurrencias.RemoveAll(r => corruptos.Contains(r));
            modificados = true;
        }

        foreach (var r in recurrencias)
        {
            if (r.Descripcion is null)
            {
                r.Descripcion = string.Empty;
                modificados = true;
            }

            if (r.ProximaFecha == default)
            {
                r.ProximaFecha = r.FechaInicio;
                modificados = true;
            }

            if (r.Intervalo <= 0)
            {
                r.Intervalo = 1;
                modificados = true;
            }
        }

        if (modificados)
            await storage.SetAsync("cdg_recurrencias", recurrencias);

        return modificados;
    }

    private static async Task<bool> NormalizarFinanciamientosAsync(IStorageService storage)
    {
        var financiamientos = await storage.GetAsync<List<Financiamiento>>("cdg_financiamientos");
        if (financiamientos is null || financiamientos.Count == 0)
            return false;

        var modificados = false;

        var corruptos = financiamientos.Where(f => f.Id == Guid.Empty).ToList();
        if (corruptos.Count > 0)
        {
            financiamientos.RemoveAll(f => corruptos.Contains(f));
            modificados = true;
        }

        foreach (var f in financiamientos)
        {
            if (f.Banco is null)
            {
                f.Banco = string.Empty;
                modificados = true;
            }

            if (f.Alias is null)
            {
                f.Alias = string.Empty;
                modificados = true;
            }

            if (f.Tipo is null)
            {
                f.Tipo = "Credito";
                modificados = true;
            }

            if (f.MontoTotal <= 0)
            {
                f.MontoTotal = 1;
                modificados = true;
            }

            if (f.PlazoMeses <= 0)
            {
                f.PlazoMeses = 1;
                modificados = true;
            }
        }

        if (modificados)
            await storage.SetAsync("cdg_financiamientos", financiamientos);

        return modificados;
    }

    private static async Task<bool> NormalizarProgresoRpgAsync(IStorageService storage)
    {
        var progreso = await storage.GetAsync<ProgresoRPG>("cdg_progreso_rpg");
        if (progreso is null)
            return false;

        var modificados = false;

        if (progreso.Id == Guid.Empty)
        {
            progreso.Id = Guid.NewGuid();
            modificados = true;
        }

        if (progreso.Nivel <= 0)
        {
            progreso.Nivel = 1;
            modificados = true;
        }

        if (progreso.ExpRequerida <= 0)
        {
            progreso.ExpRequerida = 100;
            modificados = true;
        }

        if (progreso.HpMaximo <= 0)
        {
            progreso.HpMaximo = 100;
            modificados = true;
        }

        if (progreso.HpActual < 0)
        {
            progreso.HpActual = 0;
            modificados = true;
        }

        if (modificados)
            await storage.SetAsync("cdg_progreso_rpg", progreso);

        return modificados;
    }

    private static async Task<bool> NormalizarUsuarioAsync(IStorageService storage)
    {
        var usuario = await storage.GetAsync<Usuario>("cdg_usuario");
        if (usuario is null)
            return false;

        var modificados = false;

        if (usuario.Id == Guid.Empty)
        {
            usuario.Id = Guid.NewGuid();
            modificados = true;
        }

        if (usuario.Nombre is null)
        {
            usuario.Nombre = string.Empty;
            modificados = true;
        }

        if (usuario.Moneda is null)
        {
            usuario.Moneda = "MXN";
            modificados = true;
        }

        if (usuario.PinDelaySegundos <= 0)
        {
            usuario.PinDelaySegundos = 30;
            modificados = true;
        }

        if (modificados)
            await storage.SetAsync("cdg_usuario", usuario);

        return modificados;
    }
}
