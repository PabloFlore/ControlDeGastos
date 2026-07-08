using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class TiendaService : ITiendaService
{
    private const string StorageKey = "cdg_progreso_rpg";
    private readonly IStorageService _storage;
    private readonly IGamificacionService _gamificacionService;
    private static readonly List<ArticuloTienda> Catalogo = new()
    {
        // XP Boosts
        new() { Id = "boost_xp_15_3d", Nombre = "Impulso menor", Descripcion = "Multiplica tu EXP ×1.5 durante 3 días", Icono = "⚡", Categoria = CategoriaArticulo.BoostExp, Precio = 60, EsBoost = true, TipoDuracion = TipoDuracionBoost.Dias, DuracionValor = 3, ValorNumerico = 1.5, Orden = 1 },
        new() { Id = "boost_xp_15_7d", Nombre = "Impulso menor+", Descripcion = "Multiplica tu EXP ×1.5 durante 7 días", Icono = "⚡", Categoria = CategoriaArticulo.BoostExp, Precio = 100, EsBoost = true, TipoDuracion = TipoDuracionBoost.Dias, DuracionValor = 7, ValorNumerico = 1.5, Orden = 2 },
        new() { Id = "boost_xp_15_14d", Nombre = "Impulso menor++", Descripcion = "Multiplica tu EXP ×1.5 durante 14 días", Icono = "⚡", Categoria = CategoriaArticulo.BoostExp, Precio = 150, EsBoost = true, TipoDuracion = TipoDuracionBoost.Dias, DuracionValor = 14, ValorNumerico = 1.5, Orden = 3 },
        new() { Id = "boost_xp_2_3d", Nombre = "Impulso mayor", Descripcion = "Multiplica tu EXP ×2 durante 3 días", Icono = "🚀", Categoria = CategoriaArticulo.BoostExp, Precio = 100, EsBoost = true, TipoDuracion = TipoDuracionBoost.Dias, DuracionValor = 3, ValorNumerico = 2.0, Orden = 4 },
        new() { Id = "boost_xp_2_7d", Nombre = "Impulso mayor+", Descripcion = "Multiplica tu EXP ×2 durante 7 días", Icono = "🚀", Categoria = CategoriaArticulo.BoostExp, Precio = 200, EsBoost = true, TipoDuracion = TipoDuracionBoost.Dias, DuracionValor = 7, ValorNumerico = 2.0, Orden = 5 },
        new() { Id = "boost_xp_2_14d", Nombre = "Impulso mayor++", Descripcion = "Multiplica tu EXP ×2 durante 14 días", Icono = "🚀", Categoria = CategoriaArticulo.BoostExp, Precio = 350, EsBoost = true, TipoDuracion = TipoDuracionBoost.Dias, DuracionValor = 14, ValorNumerico = 2.0, Orden = 6 },
        new() { Id = "boost_xp_3_3d", Nombre = "Impulso legendario", Descripcion = "Multiplica tu EXP ×3 durante 3 días", Icono = "💫", Categoria = CategoriaArticulo.BoostExp, Precio = 200, EsBoost = true, TipoDuracion = TipoDuracionBoost.Dias, DuracionValor = 3, ValorNumerico = 3.0, Orden = 7 },
        new() { Id = "boost_xp_3_7d", Nombre = "Impulso legendario+", Descripcion = "Multiplica tu EXP ×3 durante 7 días", Icono = "💫", Categoria = CategoriaArticulo.BoostExp, Precio = 400, EsBoost = true, TipoDuracion = TipoDuracionBoost.Dias, DuracionValor = 7, ValorNumerico = 3.0, Orden = 8 },
        new() { Id = "boost_xp_3_14d", Nombre = "Impulso legendario++", Descripcion = "Multiplica tu EXP ×3 durante 14 días", Icono = "💫", Categoria = CategoriaArticulo.BoostExp, Precio = 600, EsBoost = true, TipoDuracion = TipoDuracionBoost.Dias, DuracionValor = 14, ValorNumerico = 3.0, Orden = 9 },

        // HP Potions
        new() { Id = "pocion_pequena", Nombre = "Poción pequeña", Descripcion = "Recupera 25 HP al instante", Icono = "🧪", Categoria = CategoriaArticulo.CuracionHp, Precio = 100, EsConsumible = true, ValorNumerico = 25, Orden = 10 },
        new() { Id = "pocion_grande", Nombre = "Poción grande", Descripcion = "Recupera 50 HP al instante", Icono = "🧪", Categoria = CategoriaArticulo.CuracionHp, Precio = 200, EsConsumible = true, ValorNumerico = 50, Orden = 11 },
        new() { Id = "pocion_maxima", Nombre = "Poción máxima", Descripcion = "Recupera todo tu HP al máximo", Icono = "❤️‍🔥", Categoria = CategoriaArticulo.CuracionHp, Precio = 350, EsConsumible = true, ValorNumerico = -1, Orden = 12 },

        // Max HP expansions
        new() { Id = "expansion_hp_25", Nombre = "Amuleto de vitalidad", Descripcion = "Aumenta tu HP máximo en 25 permanentemente", Icono = "💚", Categoria = CategoriaArticulo.ExpansionHpMax, Precio = 200, ValorNumerico = 25, Orden = 13 },
        new() { Id = "expansion_hp_50", Nombre = "Corazón de dragón", Descripcion = "Aumenta tu HP máximo en 50 permanentemente", Icono = "🐉", Categoria = CategoriaArticulo.ExpansionHpMax, Precio = 350, ValorNumerico = 50, Orden = 14 },
        new() { Id = "expansion_hp_100", Nombre = "Fuente de la juventud", Descripcion = "Aumenta tu HP máximo en 100 permanentemente", Icono = "🌊", Categoria = CategoriaArticulo.ExpansionHpMax, Precio = 600, ValorNumerico = 100, Orden = 15 },

        // Streak Shield
        new() { Id = "escudo_racha", Nombre = "Escudo de Racha", Descripcion = "Protege tu racha de gastos consecutivos una vez", Icono = "🛡️", Categoria = CategoriaArticulo.EscudoRacha, Precio = 150, EsConsumible = true, Orden = 16 },

        // Mystery Box
        new() { Id = "caja_sorpresa", Nombre = "Caja", Descripcion = "Contiene un item aleatorio de la tienda", Icono = "📦", Categoria = CategoriaArticulo.CajaSorpresa, Precio = 250, EsConsumible = true, Orden = 17 },

        // Shop Titles
        new() { Id = "titulo_estratega", Nombre = "Estratega financiero", Descripcion = "Cada peso tiene un plan. Tus movimientos financieros son de libro de texto.", Icono = "🧠", Categoria = CategoriaArticulo.TituloTienda, Precio = 200, TituloId = "shop_estratega", TituloIcono = "🧠", Orden = 18 },
        new() { Id = "titulo_coleccionista", Nombre = "Coleccionista", Descripcion = "No solo acumulas monedas... acumulas leyenda. Cada artículo cuenta una historia.", Icono = "💎", Categoria = CategoriaArticulo.TituloTienda, Precio = 250, TituloId = "shop_coleccionista", TituloIcono = "💎", Orden = 19 },
        new() { Id = "titulo_millonario", Nombre = "Millonario", Descripcion = "Los ceros bailan a tu favor. Tu cuenta bancaria tiene su propia zona de confort.", Icono = "🤑", Categoria = CategoriaArticulo.TituloTienda, Precio = 300, TituloId = "shop_millonario", TituloIcono = "🤑", Orden = 20 },
        new() { Id = "titulo_rey_tienda", Nombre = "Rey de la tienda", Descripcion = "El trono de la tienda es tuyo. Los demás solo alquilan el espacio.", Icono = "👑", Categoria = CategoriaArticulo.TituloTienda, Precio = 300, TituloId = "shop_rey_tienda", TituloIcono = "👑", Orden = 21 },

        // Card Skins
        new() { Id = "skin_metal", Nombre = "Skin Metal", Descripcion = "Tarjetas con acabado metálico plateado", Icono = "⚙️", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 300, SkinCssClass = "skin-metal", Orden = 22 },
        new() { Id = "skin_tierra", Nombre = "Skin Tierra", Descripcion = "Tarjetas con textura natural y verde", Icono = "🌿", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 300, SkinCssClass = "skin-tierra", Orden = 23 },
        new() { Id = "skin_oscura", Nombre = "Skin Oscura", Descripcion = "Tarjetas de gasto con estilo oscuro profundo", Icono = "🌑", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 300, SkinCssClass = "skin-oscura", Orden = 24 },
        new() { Id = "skin_fantasma", Nombre = "Skin Fantasma", Descripcion = "Tarjetas etéreas con opacidad variable", Icono = "👻", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 300, SkinCssClass = "skin-fantasma", Orden = 25 },
        new() { Id = "skin_negro_brillante", Nombre = "Negro Brillante", Descripcion = "Tarjetas oscuras con destellos estelares", Icono = "🖤", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 600, SkinCssClass = "skin-negro-brillante", Orden = 26 },
        new() { Id = "skin_fuego", Nombre = "Skin Fuego", Descripcion = "Tarjetas con llamas y glow anaranjado", Icono = "🔥", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 600, SkinCssClass = "skin-fuego", Orden = 27 },
        new() { Id = "skin_hielo", Nombre = "Skin Hielo", Descripcion = "Tarjetas con estética de cristal y hielo", Icono = "❄️", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 600, SkinCssClass = "skin-hielo", Orden = 28 },
        new() { Id = "skin_esmeralda", Nombre = "Skin Esmeralda", Descripcion = "Tarjetas con acabado de gema esmeralda y acentos dorados", Icono = "💚", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 600, SkinCssClass = "skin-esmeralda", Orden = 29 },
        new() { Id = "skin_elite", Nombre = "Skin Élite", Descripcion = "Tarjetas de gasto premium minimalista", Icono = "💎", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 600, SkinCssClass = "skin-elite", Orden = 30 },
        new() { Id = "skin_neon", Nombre = "Skin Neon", Descripcion = "Tarjetas de gasto con bordes neón", Icono = "🌆", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 1000, SkinCssClass = "skin-neon", Orden = 31 },
        new() { Id = "skin_hacker", Nombre = "Skin Hacker", Descripcion = "Tarjetas con estética matrix verde", Icono = "💚", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 1000, SkinCssClass = "skin-hacker", Orden = 32 },
        new() { Id = "skin_oro", Nombre = "Skin Oro", Descripcion = "Tarjetas de gasto con acabado dorado", Icono = "✨", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 1000, SkinCssClass = "skin-oro", Orden = 33 },
        new() { Id = "skin_retro", Nombre = "Skin Retro", Descripcion = "Tarjetas con estilo vaporwave retro", Icono = "🕹️", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 1000, SkinCssClass = "skin-retro", Orden = 34 },
        new() { Id = "skin_mistica", Nombre = "Skin Mística", Descripcion = "Tarjetas con estética cósmica púrpura", Icono = "🔮", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 1000, SkinCssClass = "skin-mistica", Orden = 35 },
        new() { Id = "skin_dragon", Nombre = "Skin Dragón", Descripcion = "Tarjetas con textura de escamas de dragón", Icono = "🐉", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 1000, SkinCssClass = "skin-dragon", Orden = 36 },
        new() { Id = "skin_rgb", Nombre = "Skin RGB", Descripcion = "Tarjetas con animación de colores rojo, verde y azul", Icono = "💈", Categoria = CategoriaArticulo.SkinTarjeta, Precio = 1000, SkinCssClass = "skin-rgb", Orden = 37 },

        // Extended Titles
        new() { Id = "titulo_cabra", Nombre = "La CABRA", Descripcion = "El mas grande de todos los tiempos... o por cabrear?", Icono = "🐐", Categoria = CategoriaArticulo.TituloTienda, Precio = 400, TituloId = "shop_cabra", TituloIcono = "🐐", Orden = 39 },
        new() { Id = "titulo_son_of_god", Nombre = "Son of God", Descripcion = "Demuestra que no estas solo, sino caminando en El", Icono = "💫", Categoria = CategoriaArticulo.TituloTienda, Precio = 600, TituloId = "shop_son_of_god", TituloIcono = "💫", Orden = 44 },
        new() { Id = "titulo_aura_10k", Nombre = "+10,000 de aura", Descripcion = "Derramas mucha Aura.", Icono = "✨", Categoria = CategoriaArticulo.TituloTienda, Precio = 400, TituloId = "shop_aura_10k", TituloIcono = "✨", Orden = 41 },
        new() { Id = "titulo_aura_negativa", Nombre = "Aura Negativa", Descripcion = "Ya no le muevas porfa", Icono = "💀", Categoria = CategoriaArticulo.TituloTienda, Precio = 400, TituloId = "shop_aura_negativa", TituloIcono = "💀", Orden = 42 },
        new() { Id = "titulo_deudor_aura", Nombre = "Deudor de Aura", Descripcion = "Debes aura, pa", Icono = "💳", Categoria = CategoriaArticulo.TituloTienda, Precio = 400, TituloId = "shop_deudor_aura", TituloIcono = "💳", Orden = 43 },
    };

    public TiendaService(IStorageService storage, IGamificacionService gamificacionService)
    {
        _storage = storage;
        _gamificacionService = gamificacionService;
    }

    public Task<List<ArticuloTienda>> ObtenerCatalogoAsync()
    {
        return Task.FromResult(Catalogo.OrderBy(a => a.Orden).ToList());
    }

    public async Task<(bool exito, string mensaje)> ComprarItemAsync(string itemId)
    {
        var item = Catalogo.FirstOrDefault(a => a.Id == itemId);
        if (item is null)
            return (false, "El artículo no existe");

        if (item.Categoria == CategoriaArticulo.CajaSorpresa)
            return (false, "Usa el botón Comprar de la caja para abrirla");

        var progreso = await _gamificacionService.ObtenerProgresoAsync();

        if (progreso.Monedas < item.Precio)
            return (false, $"No tienes suficientes monedas. Necesitas {item.Precio} 🪙");

        switch (item.Categoria)
        {
            case CategoriaArticulo.SkinTarjeta:
                if (progreso.IdsSkinsCompradas.Contains(item.Id))
                    return (false, "Ya posees esta skin");
                progreso.IdsSkinsCompradas.Add(item.Id);
                break;

            case CategoriaArticulo.TituloTienda:
                if (progreso.IdsTitulosTienda.Contains(item.TituloId!))
                    return (false, "Ya posees este título");
                progreso.IdsTitulosTienda.Add(item.TituloId!);
                break;

            case CategoriaArticulo.BoostExp:
                var expiracion = item.TipoDuracion == TipoDuracionBoost.Dias ? DateTime.UtcNow.AddDays(item.DuracionValor) : DateTime.UtcNow.AddHours(item.DuracionValor);
                progreso.BoostExpMultiplicador = item.ValorNumerico;
                progreso.BoostExpExpiracion = expiracion;
                progreso.BoostExpItemId = item.Id;
                break;

            case CategoriaArticulo.CuracionHp:
                if (item.ValorNumerico < 0)
                    progreso.HpActual = progreso.HpMaximo;
                else
                    progreso.HpActual = Math.Min(progreso.HpActual + (int)item.ValorNumerico, progreso.HpMaximo);
                break;

            case CategoriaArticulo.ExpansionHpMax:
                if (progreso.IdsExpansionesCompradas.Contains(item.Id))
                    return (false, "Ya has adquirido esta mejora de HP");
                progreso.IdsExpansionesCompradas.Add(item.Id);
                progreso.HpMaximo += (int)item.ValorNumerico;
                progreso.HpActual = Math.Min(progreso.HpActual + (int)item.ValorNumerico, progreso.HpMaximo);
                break;

            case CategoriaArticulo.EscudoRacha:
                progreso.EscudosRacha++;
                break;
        }

        progreso.Monedas -= item.Precio;
        progreso.MonedasGastadas += item.Precio;

        var storageKey = "cdg_progreso_rpg";
        await _storage.SetAsync(storageKey, progreso);

        return (true, $"¡Has comprado {item.Nombre}!");
    }

    public async Task<(bool exito, string mensaje, ArticuloTienda? itemGanado, int compensacion)> ProcesarCajaSorpresaAsync(string itemId)
    {
        var item = Catalogo.FirstOrDefault(a => a.Id == itemId);
        if (item is null || item.Categoria != CategoriaArticulo.CajaSorpresa)
            return (false, "El artículo no existe o no es una caja sorpresa", null, 0);

        var progreso = await _gamificacionService.ObtenerProgresoAsync();

        if (progreso.Monedas < item.Precio)
            return (false, $"No tienes suficientes monedas. Necesitas {item.Precio} 🪙", null, 0);

        var rng = new Random();
        var disponibles = Catalogo
            .Where(c => c.Categoria != CategoriaArticulo.CajaSorpresa && c.Id != item.Id)
            .ToList();
        if (disponibles.Count == 0)
            return (false, "No hay items disponibles en la caja", null, 0);

        var elegido = disponibles[rng.Next(disponibles.Count)];
        var yaPoseido = ItemPoseido(progreso, elegido);

        var esRepetido = yaPoseido;
        var motivoCompensacion = "Ya lo tenías";

        if (!esRepetido && elegido.Categoria == CategoriaArticulo.BoostExp
            && progreso.BoostExpExpiracion.HasValue
            && progreso.BoostExpExpiracion.Value > DateTime.UtcNow)
        {
            var nuevaDuracion = elegido.TipoDuracion == TipoDuracionBoost.Dias
                ? TimeSpan.FromDays(elegido.DuracionValor)
                : TimeSpan.FromHours(elegido.DuracionValor);
            var restanteActual = progreso.BoostExpExpiracion.Value - DateTime.UtcNow;
            var esMejor = elegido.ValorNumerico > progreso.BoostExpMultiplicador
                || (elegido.ValorNumerico == progreso.BoostExpMultiplicador && nuevaDuracion > restanteActual);

            if (!esMejor)
            {
                esRepetido = true;
                motivoCompensacion = "Tienes un boost igual o mejor";
            }
        }

        if (!esRepetido && elegido.Categoria == CategoriaArticulo.CuracionHp
            && progreso.HpActual >= progreso.HpMaximo)
        {
            esRepetido = true;
            motivoCompensacion = "Ya tienes la vida al máximo";
        }

        progreso.Monedas -= item.Precio;
        progreso.MonedasGastadas += item.Precio;

        if (esRepetido)
        {
            var compensacion = Math.Max(1, elegido.Precio / 4);
            progreso.Monedas += compensacion;
            await _storage.SetAsync(StorageKey, progreso);
            return (true, $"¡Te salió {elegido.Nombre}! {motivoCompensacion}, +{compensacion} 🪙", elegido, compensacion);
        }

        OtorgarItem(progreso, elegido);
        await _storage.SetAsync(StorageKey, progreso);
        return (true, $"¡Felicidades! Ganaste: {elegido.Nombre} {elegido.Icono}", elegido, 0);
    }

    public async Task<bool> EquiparSkinAsync(string? skinId)
    {
        var progreso = await _gamificacionService.ObtenerProgresoAsync();

        if (skinId is not null && !progreso.IdsSkinsCompradas.Contains(skinId))
            return false;

        var item = Catalogo.FirstOrDefault(a => a.Id == skinId);
        progreso.SkinTarjetaActiva = skinId is not null ? item?.SkinCssClass : null;
        await _storage.SetAsync("cdg_progreso_rpg", progreso);
        return true;
    }

    public async Task<bool> EquiparTituloTiendaAsync(string tituloId)
    {
        var progreso = await _gamificacionService.ObtenerProgresoAsync();

        if (!progreso.IdsTitulosTienda.Contains(tituloId))
            return false;

        var mismoId = progreso.TituloActivoId == tituloId;
        progreso.TituloActivoId = mismoId ? null : tituloId;
        await _storage.SetAsync("cdg_progreso_rpg", progreso);
        return true;
    }

    public async Task<string?> ObtenerSkinActivaAsync()
    {
        var progreso = await _gamificacionService.ObtenerProgresoAsync();
        return progreso.SkinTarjetaActiva;
    }

    public async Task<List<string>> ObtenerSkinsCompradasAsync()
    {
        var progreso = await _gamificacionService.ObtenerProgresoAsync();
        return progreso.IdsSkinsCompradas;
    }

    public async Task<List<string>> ObtenerTitulosTiendaCompradosAsync()
    {
        var progreso = await _gamificacionService.ObtenerProgresoAsync();
        return progreso.IdsTitulosTienda;
    }

    public async Task<(double multiplicador, DateTime? expiracion, string? itemId)> ObtenerBoostExpActivoAsync()
    {
        var progreso = await _gamificacionService.ObtenerProgresoAsync();

        if (progreso.BoostExpExpiracion.HasValue && progreso.BoostExpExpiracion.Value < DateTime.UtcNow)
        {
            progreso.BoostExpMultiplicador = 1.0;
            progreso.BoostExpExpiracion = null;
            progreso.BoostExpItemId = null;
            await _storage.SetAsync("cdg_progreso_rpg", progreso);
            return (1.0, null, null);
        }

        return (progreso.BoostExpMultiplicador, progreso.BoostExpExpiracion, progreso.BoostExpItemId);
    }

    public async Task<int> ObtenerEscudoHpRestanteAsync()
    {
        var progreso = await _gamificacionService.ObtenerProgresoAsync();
        return progreso.HpEscudoRestante;
    }

    public async Task<int> ObtenerEscudosRachaAsync()
    {
        var progreso = await _gamificacionService.ObtenerProgresoAsync();
        return progreso.EscudosRacha;
    }

    private static bool ItemPoseido(ProgresoRPG progreso, ArticuloTienda item) => item.Categoria switch
    {
        CategoriaArticulo.SkinTarjeta => progreso.IdsSkinsCompradas.Contains(item.Id),
        CategoriaArticulo.TituloTienda => progreso.IdsTitulosTienda.Contains(item.TituloId!),
        CategoriaArticulo.ExpansionHpMax => progreso.IdsExpansionesCompradas.Contains(item.Id),
        _ => false
    };

    private static void OtorgarItem(ProgresoRPG progreso, ArticuloTienda item)
    {
        switch (item.Categoria)
        {
            case CategoriaArticulo.SkinTarjeta:
                if (!progreso.IdsSkinsCompradas.Contains(item.Id))
                    progreso.IdsSkinsCompradas.Add(item.Id);
                break;
            case CategoriaArticulo.TituloTienda:
                if (!progreso.IdsTitulosTienda.Contains(item.TituloId!))
                    progreso.IdsTitulosTienda.Add(item.TituloId!);
                break;
            case CategoriaArticulo.ExpansionHpMax:
                if (!progreso.IdsExpansionesCompradas.Contains(item.Id))
                {
                    progreso.IdsExpansionesCompradas.Add(item.Id);
                    progreso.HpMaximo += (int)item.ValorNumerico;
                    progreso.HpActual = Math.Min(progreso.HpActual + (int)item.ValorNumerico, progreso.HpMaximo);
                }
                break;
            case CategoriaArticulo.EscudoRacha:
                progreso.EscudosRacha++;
                break;
            case CategoriaArticulo.BoostExp:
                var expiracion = item.TipoDuracion == TipoDuracionBoost.Dias ? DateTime.UtcNow.AddDays(item.DuracionValor) : DateTime.UtcNow.AddHours(item.DuracionValor);
                progreso.BoostExpMultiplicador = item.ValorNumerico;
                progreso.BoostExpExpiracion = expiracion;
                progreso.BoostExpItemId = item.Id;
                break;
            case CategoriaArticulo.CuracionHp:
                if (item.ValorNumerico < 0)
                    progreso.HpActual = progreso.HpMaximo;
                else
                    progreso.HpActual = Math.Min(progreso.HpActual + (int)item.ValorNumerico, progreso.HpMaximo);
                break;
        }
    }
}
