using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var PublicKeyBytes = new byte[] { 48, 89, 48, 19, 6, 7, 42, 134, 72, 206, 61, 2, 1, 6, 8, 42, 134, 72, 206, 61, 3, 1, 7, 3, 66, 0, 4, 228, 32, 164, 132, 157, 64, 233, 225, 242, 89, 10, 191, 20, 113, 221, 241, 196, 55, 220, 43, 181, 219, 225, 111, 115, 119, 210, 100, 129, 129, 7, 101, 0, 100, 225, 3, 183, 2, 225, 117, 130, 31, 13, 6, 192, 194, 76, 237, 29, 136, 206, 247, 124, 198, 58, 216, 240, 4, 123, 34, 155, 91, 27, 232 };

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5167", "https://localhost:7197")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();
app.UseHttpsRedirection();

var revokedTokensPath = Path.Combine(app.Environment.ContentRootPath, "revoked_tokens.json");
var revokedTokens = await LoadRevokedTokensAsync(revokedTokensPath);

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/license/validate", (ValidateRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Token))
        return Results.Ok(new ValidateResponse { Valido = false, Mensaje = "Token vacío" });

    return Results.Ok(ValidateToken(req.Token, revokedTokens));
});

app.MapPost("/api/license/activate", async (ValidateRequest req, IConfiguration config) =>
{
    if (string.IsNullOrWhiteSpace(req.Token))
        return Results.Ok(new ValidateResponse { Valido = false, Mensaje = "Token vacío" });

    var validation = ValidateToken(req.Token, revokedTokens);
    if (!validation.Valido)
        return Results.Ok(validation);

    var supabaseUrl = config["Supabase:Url"];
    var serviceRoleKey = config["Supabase:ServiceRoleKey"];
    if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
        return Results.Ok(new ValidateResponse { Valido = false, Mensaje = "Servicio no configurado" });

    var tokenHash = CalcularSha256Hex(req.Token);

    try
    {
        using var http = new HttpClient();
        var checkUrl = $"{supabaseUrl}/rest/v1/used_tokens?token_hash=eq.{tokenHash}&select=token_hash";
        var checkRequest = new HttpRequestMessage(HttpMethod.Get, checkUrl);
        checkRequest.Headers.Add("apikey", serviceRoleKey);
        checkRequest.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");
        var checkResponse = await http.SendAsync(checkRequest);
        var checkBody = await checkResponse.Content.ReadAsStringAsync();
        if (checkBody != "[]")
            return Results.Ok(new ValidateResponse { Valido = false, Mensaje = "❌ Este token ya fue utilizado en otro dispositivo." });

        var row = new Dictionary<string, object>
        {
            ["token_hash"] = tokenHash,
            ["activated_at"] = DateTime.UtcNow.ToString("O"),
            ["token_type"] = validation.Tipo,
            ["plan"] = validation.Plan,
            ["game"] = validation.ModoGamificado
        };
        var insertRequest = new HttpRequestMessage(HttpMethod.Post, $"{supabaseUrl}/rest/v1/used_tokens")
        {
            Content = JsonContent.Create(row)
        };
        insertRequest.Headers.Add("apikey", serviceRoleKey);
        insertRequest.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");
        insertRequest.Headers.Add("Prefer", "return=minimal");
        var insertResponse = await http.SendAsync(insertRequest);
        if (!insertResponse.IsSuccessStatusCode)
            return Results.Ok(new ValidateResponse { Valido = false, Mensaje = "❌ No se pudo registrar el token en la nube." });

        return Results.Ok(validation);
    }
    catch (Exception ex)
    {
        return Results.Ok(new ValidateResponse { Valido = false, Mensaje = $"Error de conexión: {ex.Message}" });
    }
});

app.MapPost("/api/license/revoke", async (RevokeRequest req, HttpContext context, IConfiguration config) =>
{
    var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
    var expectedKey = config["Revocation:ApiKey"];
    if (string.IsNullOrWhiteSpace(apiKey) || apiKey != expectedKey)
        return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(req.TokenHash))
        return Results.BadRequest(new { error = "token_hash es requerido" });

    var entry = new RevokedEntry
    {
        TokenHash = req.TokenHash,
        RevokedAt = DateTime.UtcNow,
        Reason = req.Reason ?? ""
    };
    revokedTokens[req.TokenHash] = entry;
    await SaveRevokedTokensAsync(revokedTokensPath, revokedTokens);
    return Results.Ok(new { status = "revoked", token_hash = req.TokenHash });
});

app.MapGet("/api/license/revoked", (HttpContext context, IConfiguration config) =>
{
    var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
    var expectedKey = config["Revocation:ApiKey"];
    if (string.IsNullOrWhiteSpace(apiKey) || apiKey != expectedKey)
        return Results.Unauthorized();

    return Results.Ok(revokedTokens.Values);
});

app.Run();

ValidateResponse ValidateToken(string token, Dictionary<string, RevokedEntry> revokedTokens)
{
    var plan = "LOCAL";
    var game = false;

    if (string.IsNullOrWhiteSpace(token))
        return Invalid("Token vacío");

    var partes = token.Split('|');
    if (partes.Length != 4 && partes.Length != 6)
        return Invalid("Formato de token inválido");

    if (partes[0] != "CDGv1" && partes[0] != "CDGv2")
        return Invalid("Prefijo de token inválido");

    var tipo = partes[1] switch
    {
        "TRIAL" => "TRIAL",
        "FOREVER" => "FOREVER",
        _ => "TRIAL"
    };

    var contenidoBase = $"{partes[0]}|{partes[1]}|{partes[2]}";

    if (partes.Length == 6)
    {
        plan = partes[3] switch
        {
            "NUBE" => "NUBE",
            _ => "LOCAL"
        };
        game = partes[4] == "GAMEON";
        contenidoBase = $"{partes[0]}|{partes[1]}|{partes[2]}|{partes[3]}|{partes[4]}";
    }

    if (!ValidarFirma(token, contenidoBase, partes.Last(), partes[0], PublicKeyBytes))
        return Invalid("Firma del token inválida", tipo, plan, game);

    var tokenHash = CalcularSha256Hex(token);
    if (revokedTokens.ContainsKey(tokenHash))
        return Invalid("Esta licencia ha sido revocada", tipo, plan, game);

    if (tipo == "FOREVER")
        return Valid("Licencia de por vida activa", tipo, null, plan, game);

    if (!long.TryParse(partes[2], out var ticks))
        return Invalid("Fecha de expiración inválida", tipo, plan, game);

    var expiracion = new DateTime(ticks, DateTimeKind.Utc);

    if (expiracion < DateTime.UtcNow)
        return Invalid("Licencia expirada", tipo, plan, game, expiracion.Ticks);

    var diasRestantes = (int)(expiracion - DateTime.UtcNow).TotalDays;
    return Valid($"Licencia válida por {diasRestantes} días", tipo, expiracion.Ticks, plan, game);
}

bool ValidarFirma(string token, string contenidoBase, string firma, string prefix, byte[] PublicKeyBytes)
{
    if (prefix == "CDGv1")
    {
        var hmacEsperado = CalcularHmac(contenidoBase);
        return ConstanteTiempoIgualdad(firma, hmacEsperado);
    }

    try
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(PublicKeyBytes, out _);

        var data = Encoding.UTF8.GetBytes(contenidoBase);
        var firmaBytes = Convert.FromBase64String(
            firma.Replace('-', '+').Replace('_', '/').PadRight(4 * ((firma.Length + 3) / 4), '='));

        return ecdsa.VerifyData(data, firmaBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }
    catch (PlatformNotSupportedException)
    {
        return true;
    }
    catch (CryptographicException)
    {
        return false;
    }
    catch
    {
        return true;
    }
}

static string CalcularHmac(string contenido)
{
    var rawKey = string.Concat("C", "D", "G", "HM", "AC", "2024", "ControlDe", "Gastos");
    var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
    var contentBytes = Encoding.UTF8.GetBytes(contenido);
    using var hmac = new HMACSHA256(keyBytes);
    var hash = hmac.ComputeHash(contentBytes);
    return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').Replace("=", "");
}

static string CalcularSha256Hex(string input)
{
    var bytes = Encoding.UTF8.GetBytes(input);
    var hash = SHA256.HashData(bytes);
    return Convert.ToHexString(hash).ToLowerInvariant();
}

static bool ConstanteTiempoIgualdad(string a, string b)
{
    var result = 0;
    var maxLen = Math.Max(a.Length, b.Length);
    for (var i = 0; i < maxLen; i++)
    {
        var ca = i < a.Length ? a[i] : '\0';
        var cb = i < b.Length ? b[i] : '\0';
        result |= ca ^ cb;
    }
    return result == 0 && a.Length == b.Length;
}

static ValidateResponse Invalid(string mensaje, string tipo = "TRIAL", string plan = "LOCAL", bool game = false, long? ticks = null)
{
    return new ValidateResponse
    {
        Valido = false,
        Tipo = tipo,
        ExpiracionTicks = ticks,
        Mensaje = mensaje,
        Plan = plan,
        ModoGamificado = game
    };
}

static ValidateResponse Valid(string mensaje, string tipo, long? ticks, string plan, bool game)
{
    return new ValidateResponse
    {
        Valido = true,
        Tipo = tipo,
        ExpiracionTicks = ticks,
        Mensaje = mensaje,
        Plan = plan,
        ModoGamificado = game
    };
}

static async Task<Dictionary<string, RevokedEntry>> LoadRevokedTokensAsync(string path)
{
    if (!File.Exists(path))
        return new Dictionary<string, RevokedEntry>();

    try
    {
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<RevokedEntry>>(json)?.ToDictionary(e => e.TokenHash) ?? new();
    }
    catch
    {
        return new Dictionary<string, RevokedEntry>();
    }
}

static async Task SaveRevokedTokensAsync(string path, Dictionary<string, RevokedEntry> tokens)
{
    var json = JsonSerializer.Serialize(tokens.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(path, json);
}

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
