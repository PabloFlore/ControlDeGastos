using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class NotificacionService : INotificacionService
{
    private const string StorageKey = "cdg_notificaciones_vistas";
    private const string VistasMapKey = "cdg_notif_vistas_map";
    private readonly IStorageService _storage;
    private readonly IRecurrenciaService _recurrenciaService;
    private readonly IPresupuestoService _presupuestoService;
    private readonly ICategoriaService _categoriaService;

    private readonly HashSet<Guid> _mostradasEnSesion = new();

    public NotificacionService(
        IStorageService storage,
        IRecurrenciaService recurrenciaService,
        IPresupuestoService presupuestoService,
        ICategoriaService categoriaService)
    {
        _storage = storage;
        _recurrenciaService = recurrenciaService;
        _presupuestoService = presupuestoService;
        _categoriaService = categoriaService;
    }

    public async Task<List<Notificacion>> VerificarNotificacionesAsync()
    {
        var notificaciones = new List<Notificacion>();
        var hoy = DateTime.Now.Date;

        notificaciones.AddRange(await VerificarRecurrencias(hoy));
        notificaciones.AddRange(await VerificarPresupuestos(hoy));

        var idsVistos = await ObtenerIdsVistosHoyAsync();
        notificaciones.RemoveAll(n => idsVistos.Contains(n.Id) || _mostradasEnSesion.Contains(n.Id));

        foreach (var n in notificaciones)
            _mostradasEnSesion.Add(n.Id);

        if (notificaciones.Count > 0)
            await GuardarIdsVistosHoyAsync(notificaciones.Select(n => n.Id));

        return notificaciones;
    }

    private async Task<List<Notificacion>> VerificarRecurrencias(DateTime hoy)
    {
        var result = new List<Notificacion>();
        var recurrencias = await _recurrenciaService.ObtenerRecurrenciasAsync();
        var dentroDe3Dias = hoy.AddDays(3);

        foreach (var r in recurrencias)
        {
            if (!r.Activa) continue;
            if (r.FechaFin.HasValue && r.FechaFin.Value <= hoy) continue;
            if (r.ProximaFecha.Date > dentroDe3Dias) continue;
            if (r.ProximaFecha.Date < hoy) continue;

            var diasRestantes = (r.ProximaFecha.Date - hoy).Days;
            var cuando = diasRestantes switch
            {
                0 => "hoy",
                1 => "mañana",
                _ => $"en {diasRestantes} días"
            };

            var montoStr = $"${r.Monto:N2}";
            var desc = !string.IsNullOrWhiteSpace(r.Descripcion) ? $" ({r.Descripcion})" : "";
            var mensaje = $"Vence {cuando}: {montoStr}{desc}";

            result.Add(new Notificacion
            {
                Id = GuidFromKey("recurrencia", r.Id),
                Tipo = "recurrencia",
                Mensaje = mensaje,
                Icono = "📅",
                Fecha = DateTime.UtcNow,
                ReferenciaId = r.Id,
            });
        }

        return result;
    }

    private async Task<List<Notificacion>> VerificarPresupuestos(DateTime hoy)
    {
        var result = new List<Notificacion>();
        var categorias = await _categoriaService.ObtenerCategoriasAsync();
        var cacheCats = categorias.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
        var presupuestos = await _presupuestoService.ObtenerPresupuestosAsync();

        foreach (var p in presupuestos)
        {
            var gastado = await _presupuestoService.ObtenerGastadoEnPeriodoAsync(p);
            if (gastado <= 0) continue;

            var porcentaje = (int)Math.Round(gastado * 100 / p.MontoLimite);
            var catNombre = p.CategoriaId.HasValue
                ? cacheCats.GetValueOrDefault(p.CategoriaId.Value)?.Nombre ?? "Sin categoría"
                : "General";

            if (porcentaje >= 100)
            {
                var exceso = gastado - p.MontoLimite;
                result.Add(new Notificacion
                {
                    Id = GuidFromKey("presupuesto_excedido", p.Id),
                    Tipo = "presupuesto_excedido",
                    Mensaje = $"Presupuesto de {catNombre} excedido por ${exceso:N2}",
                    Icono = "🔥",
                    Fecha = DateTime.UtcNow,
                    ReferenciaId = p.Id,
                });
            }
            else if (porcentaje >= 80)
            {
                var restante = p.MontoLimite - gastado;
                result.Add(new Notificacion
                {
                    Id = GuidFromKey("presupuesto_alerta", p.Id),
                    Tipo = "presupuesto_alerta",
                    Mensaje = $"Has usado el {porcentaje}% de {catNombre}. Te quedan ${restante:N2}",
                    Icono = "⚠️",
                    Fecha = DateTime.UtcNow,
                    ReferenciaId = p.Id,
                });
            }
        }

        return result;
    }

    private async Task<HashSet<Guid>> ObtenerIdsVistosHoyAsync()
    {
        var map = await _storage.GetAsync<Dictionary<string, List<Guid>>>(VistasMapKey) ?? new();
        var hoy = DateTime.Now.Date.ToString("yyyyMMdd");

        // Limpiar entradas viejas (>7 días)
        var umbral = DateTime.Now.Date.AddDays(-7);
        var viejos = map.Keys
            .Where(k => DateTime.TryParseExact(k, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var f) && f < umbral)
            .ToList();
        foreach (var v in viejos) map.Remove(v);

        if (!map.TryGetValue(hoy, out var ids))
        {
            ids = new List<Guid>();
            map[hoy] = ids;
        }

        await _storage.SetAsync(VistasMapKey, map);
        return new HashSet<Guid>(ids);
    }

    private async Task GuardarIdsVistosHoyAsync(IEnumerable<Guid> nuevosIds)
    {
        var map = await _storage.GetAsync<Dictionary<string, List<Guid>>>(VistasMapKey) ?? new();
        var hoy = DateTime.Now.Date.ToString("yyyyMMdd");
        if (!map.TryGetValue(hoy, out var ids))
        {
            ids = new List<Guid>();
            map[hoy] = ids;
        }
        foreach (var id in nuevosIds)
        {
            if (!ids.Contains(id))
                ids.Add(id);
        }
        await _storage.SetAsync(VistasMapKey, map);
    }

    private static Guid GuidFromKey(string tipo, Guid referenciaId)
    {
        var source = $"{tipo}:{referenciaId:N}";
        var bytes = System.Text.Encoding.UTF8.GetBytes(source);
        var guidBytes = new byte[16];
        for (int i = 0; i < Math.Min(bytes.Length, 16); i++)
            guidBytes[i] = bytes[i];
        return new Guid(guidBytes);
    }
}
