using System.Text.Json.Serialization;
using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class HogarService : IHogarService
{
    private readonly ISupabaseService _supabase;
    private readonly IUsuarioService _usuarioService;
    private readonly ILogger<HogarService> _logger;

    public HogarService(ISupabaseService supabase, IUsuarioService usuarioService, ILogger<HogarService> logger)
    {
        _supabase = supabase;
        _usuarioService = usuarioService;
        _logger = logger;
    }

    public async Task<Hogar?> ObtenerHogarAsync(string hogarId)
    {
        var hogares = await _supabase.ObtenerTodosAsync<HogarRow>("hogares", $"id=eq.{Uri.EscapeDataString(hogarId)}");
        return hogares.Count > 0 ? MapHogar(hogares[0]) : null;
    }

    public async Task<Hogar?> ObtenerHogarPorCodigoAsync(string codigo)
    {
        var hogares = await _supabase.ObtenerTodosAsync<HogarRow>("hogares", $"codigo_invitacion=eq.{Uri.EscapeDataString(codigo.Trim().ToUpperInvariant())}");
        return hogares.Count > 0 ? MapHogar(hogares[0]) : null;
    }

    public async Task<List<HogarMiembro>> ObtenerMiembrosAsync(string hogarId)
    {
        var rows = await _supabase.ObtenerTodosAsync<MiembroRow>("hogar_miembros", $"hogar_id=eq.{Uri.EscapeDataString(hogarId)}");
        return rows.Select(r => new HogarMiembro
        {
            HogarId = r.HogarId,
            Email = r.Email,
            Nombre = r.Nombre,
            Avatar = r.Avatar,
            Color = r.Color,
            UsuarioId = !string.IsNullOrEmpty(r.UsuarioId) ? Guid.Parse(r.UsuarioId) : null,
            JoinedAt = DateTime.Parse(r.JoinedAt, null, System.Globalization.DateTimeStyles.RoundtripKind)
        }).ToList();
    }

    public async Task<Hogar> CrearHogarAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        var emailSesion = await _supabase.ObtenerEmailSesionAsync();
        var email = emailSesion ?? usuario?.Email;
        
        _logger?.LogInformation("CrearHogarAsync: emailSesion={EmailSesion}, usuarioEmail={UsuarioEmail}, emailFinal={EmailFinal}", 
            emailSesion, usuario?.Email, email);
        
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("No hay email de sesión disponible");

        var codigo = GenerarCodigo();
        var hogarRow = new HogarRow
        {
            Id = Guid.NewGuid().ToString(),
            CodigoInvitacion = codigo,
            CreadoPorEmail = email,
            CreatedAt = DateTime.UtcNow.ToString("O")
        };

        await _supabase.GuardarAsync("hogares", hogarRow);

        var miembroRow = new MiembroRow
        {
            HogarId = hogarRow.Id,
            Email = email,
            Nombre = usuario?.Nombre ?? email.Split('@')[0],
            Avatar = "👤",
            Color = GenerarColor(),
            UsuarioId = usuario?.Id.ToString(),
            JoinedAt = DateTime.UtcNow.ToString("O")
        };
        await _supabase.GuardarAsync("hogar_miembros", miembroRow);

        return new Hogar
        {
            Id = hogarRow.Id,
            CodigoInvitacion = codigo,
            CreadoPorEmail = email,
            CreatedAt = DateTime.UtcNow
        };
    }

    public async Task<bool> UnirseAHogarAsync(string codigo, string email)
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        var hogar = await ObtenerHogarPorCodigoAsync(codigo);
        if (hogar == null) return false;

        var miembros = await ObtenerMiembrosAsync(hogar.Id);
        if (miembros.Any(m => m.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            return false;

        var miembroRow = new MiembroRow
        {
            HogarId = hogar.Id,
            Email = email,
            Nombre = usuario?.Nombre ?? email.Split('@')[0],
            Avatar = "👤",
            Color = GenerarColor(),
            UsuarioId = usuario?.Id.ToString(),
            JoinedAt = DateTime.UtcNow.ToString("O")
        };
        await _supabase.GuardarAsync("hogar_miembros", miembroRow);
        return true;
    }

    public async Task GuardarLicenciaHogarAsync(string hogarId, Licencia licencia)
    {
        try
        {
            var row = new HogarRow
            {
                Id = hogarId,
                TokenHash = licencia.TokenHash,
                LicenciaTipo = licencia.LicenciaTipo.ToString(),
                FechaExpiracion = licencia.FechaExpiracion?.ToString("O"),
                ModoGamificadoIncluido = licencia.ModoGamificadoIncluido,
                PlanIncluido = licencia.PlanIncluido.ToString()
            };
            await _supabase.ActualizarAsync("hogares", hogarId, row);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Error al guardar licencia del hogar en la nube"); }
    }

    public async Task<Licencia?> ObtenerLicenciaHogarAsync(string hogarId)
    {
        try
        {
            var rows = await _supabase.ObtenerTodosAsync<HogarRow>("hogares", $"id=eq.{hogarId}");
            if (rows.Count == 0) return null;

            var row = rows[0];
            if (!Enum.TryParse<TipoLicencia>(row.LicenciaTipo, out var tipo))
                tipo = TipoLicencia.Trial;
            if (!Enum.TryParse<PlanType>(row.PlanIncluido, out var plan))
                plan = PlanType.Nube;

            DateTime? exp = null;
            if (!string.IsNullOrEmpty(row.FechaExpiracion) && DateTime.TryParse(row.FechaExpiracion, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expParsed))
                exp = expParsed;

            return new Licencia
            {
                Token = $"HOGAR-{hogarId}",
                TokenHash = row.TokenHash,
                LicenciaTipo = tipo,
                FechaExpiracion = exp,
                FechaActivacion = DateTime.UtcNow,
                UltimaValidacion = DateTime.UtcNow,
                Valida = true,
                Mensaje = "Licencia del hogar",
                PlanIncluido = plan,
                ModoGamificadoIncluido = row.ModoGamificadoIncluido
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al obtener licencia del hogar desde la nube");
            return null;
        }
    }

    public async Task<bool> SalirDelHogarAsync(string hogarId, string email)
    {
        try
        {
            var hogar = await ObtenerHogarAsync(hogarId);
            if (hogar is null) return false;
            if (hogar.CreadoPorEmail.Equals(email, StringComparison.OrdinalIgnoreCase))
                return false;

            await _supabase.EliminarConFiltroAsync<MiembroRow>("hogar_miembros", $"hogar_id=eq.{Uri.EscapeDataString(hogarId)}&email=eq.{Uri.EscapeDataString(email)}");
            return true;
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Error al salir del hogar en la nube"); return false; }
    }

    private string GenerarCodigo()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        return new string(Enumerable.Range(0, 8).Select(_ => chars[Random.Shared.Next(chars.Length)]).ToArray());
    }

    private string GenerarColor()
    {
        var colores = new[] { "#E91E63", "#9C27B0", "#673AB7", "#3F51B5", "#2196F3", "#00BCD4", "#009688", "#4CAF50", "#8BC34A", "#FF9800", "#FF5722", "#795548" };
        return colores[Random.Shared.Next(colores.Length)];
    }

    private static Hogar MapHogar(HogarRow row) => new()
    {
        Id = row.Id,
        CodigoInvitacion = row.CodigoInvitacion,
        CreadoPorEmail = row.CreadoPorEmail,
        CreatedAt = DateTime.Parse(row.CreatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind),
        TokenHash = row.TokenHash,
        LicenciaTipo = Enum.TryParse<TipoLicencia>(row.LicenciaTipo, out var t) ? t : TipoLicencia.Trial,
        FechaExpiracion = !string.IsNullOrEmpty(row.FechaExpiracion) && DateTime.TryParse(row.FechaExpiracion, null, System.Globalization.DateTimeStyles.RoundtripKind, out var exp) ? exp : null,
        ModoGamificadoIncluido = row.ModoGamificadoIncluido,
        PlanIncluido = Enum.TryParse<PlanType>(row.PlanIncluido, out var p) ? p : PlanType.Nube
    };

    internal class HogarRow
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
        [JsonPropertyName("codigo_invitacion")]
        public string CodigoInvitacion { get; set; } = "";
        [JsonPropertyName("creado_por_email")]
        public string CreadoPorEmail { get; set; } = "";
        [JsonPropertyName("created_at")]
        public string CreatedAt { get; set; } = "";
        [JsonPropertyName("token_hash")]
        public string? TokenHash { get; set; }
        [JsonPropertyName("licencia_tipo")]
        public string? LicenciaTipo { get; set; }
        [JsonPropertyName("fecha_expiracion")]
        public string? FechaExpiracion { get; set; }
        [JsonPropertyName("modo_gamificado_incluido")]
        public bool ModoGamificadoIncluido { get; set; }
        [JsonPropertyName("plan_incluido")]
        public string? PlanIncluido { get; set; }
    }

    internal class MiembroRow
    {
        [JsonPropertyName("hogar_id")]
        public string HogarId { get; set; } = "";
        [JsonPropertyName("email")]
        public string Email { get; set; } = "";
        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = "";
        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }
        [JsonPropertyName("color")]
        public string? Color { get; set; }
        [JsonPropertyName("usuario_id")]
        public string? UsuarioId { get; set; }
        [JsonPropertyName("joined_at")]
        public string JoinedAt { get; set; } = "";
    }
}
