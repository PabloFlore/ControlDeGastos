using System.Text.Json.Serialization;

namespace ControlDeGastos.Functions;

public class ValidateRequest
{
    public string Token { get; set; } = "";
}

public class ValidateResponse
{
    public bool Valido { get; set; }
    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = "";
    [JsonPropertyName("expiracionTicks")]
    public long? ExpiracionTicks { get; set; }
    public string Mensaje { get; set; } = "";
    [JsonPropertyName("plan")]
    public string Plan { get; set; } = "";
    [JsonPropertyName("modoGamificado")]
    public bool ModoGamificado { get; set; }
}

public class RevokeRequest
{
    public string TokenHash { get; set; } = "";
    public string? Reason { get; set; }
}

public class RevokedEntry
{
    public string TokenHash { get; set; } = "";
    public DateTime RevokedAt { get; set; }
    public string Reason { get; set; } = "";
}
