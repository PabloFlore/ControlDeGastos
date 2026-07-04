using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class LicenciaService : ILicenciaService
{
    private const string StorageKey = "cdg_licencia";
    private const string UsedTokensKey = "cdg_used_tokens";
    private const string LastValidatedKey = "cdg_last_validated";
    private const int TrialDays = 180;
    private const int GracePeriodDays = 7;
    private readonly byte[] _publicKeyBytes;
    private readonly IStorageService _storage;
    private readonly ILogger<LicenciaService> _logger;
    private readonly HttpClient _http;
    private ECDsa? _verifier;

    public LicenciaService(IStorageService storage, ILogger<LicenciaService> logger, byte[] publicKeyBytes, HttpClient http)
    {
        _storage = storage;
        _logger = logger;
        _publicKeyBytes = publicKeyBytes;
        _http = http;
    }

    private ECDsa GetVerifier()
    {
        if (_verifier != null) return _verifier;
        _verifier = ECDsa.Create();
        _verifier.ImportSubjectPublicKeyInfo(_publicKeyBytes, out _);
        return _verifier;
    }

    public async Task<Licencia> ActivarLicenciaAsync(string token)
    {
        token = token.Trim();

        var (valido, tipo, expiracion, mensaje, plan, game) = ValidarToken(token);
        if (!valido)
            return new Licencia { Valida = false, Mensaje = mensaje };

        if (!ValidarFirmaECDSA(token))
            return new Licencia { Valida = false, Mensaje = "Firma del token inválida. El token podría estar corrupto." };

        var tokenHash = CalcularSha256Hex(token);

        var supabaseCheck = await VerificarTokenEnSupabaseAsync(tokenHash);
        if (supabaseCheck == true)
            return new Licencia { Valida = false, Mensaje = "❌ Este token ya fue utilizado en otro dispositivo." };

        var usados = await _storage.GetAsync<List<string>>(UsedTokensKey) ?? new();
        if (usados.Contains(tokenHash))
            return new Licencia { Valida = false, Mensaje = "❌ Este token ya fue utilizado. No se puede volver a activar." };

        try
        {
            var response = await _http.PostAsJsonAsync("/api/license/activate", new { token });
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ValidateResponse>();
                if (result != null && result.Valido)
                {
                    usados.Add(tokenHash);
                    await _storage.SetAsync(UsedTokensKey, usados);

                    var licencia = new Licencia
                    {
                        Token = token,
                        TokenHash = tokenHash,
                        LicenciaTipo = tipo,
                        FechaExpiracion = expiracion,
                        FechaActivacion = DateTime.UtcNow,
                        UltimaValidacion = DateTime.UtcNow,
                        Valida = true,
                        Mensaje = "Licencia activada correctamente (verificada en el servidor)",
                        PlanIncluido = plan,
                        ModoGamificadoIncluido = game
                    };

                    await _storage.SetAsync(StorageKey, licencia);
                    await _storage.SetAsync(LastValidatedKey, DateTime.UtcNow);
                    return licencia;
                }

                return new Licencia { Valida = false, Mensaje = result?.Mensaje ?? "Error al activar la licencia en el servidor" };
            }

            if (plan == PlanType.Nube)
                return new Licencia { Valida = false, Mensaje = "❌ No se pudo activar el token en la nube. Verifica tu conexión." };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al activar licencia vía API");

            if (plan == PlanType.Nube)
                return new Licencia { Valida = false, Mensaje = "❌ No se pudo activar el token en la nube. Verifica tu conexión." };
        }

        try
        {
            var row = new { token_hash = tokenHash, activated_at = DateTime.UtcNow.ToString("O"), token_type = tipo.ToString(), plan = plan.ToString(), game = game };
            using var insertReq = new HttpRequestMessage(HttpMethod.Post, $"{SupabaseConfig.Url}/rest/v1/used_tokens")
            {
                Content = JsonContent.Create(row)
            };
            insertReq.Headers.Add("apikey", SupabaseConfig.AnonKey);
            insertReq.Headers.Add("Authorization", $"Bearer {SupabaseConfig.AnonKey}");
            insertReq.Headers.Add("Prefer", "return=minimal");
            await _http.SendAsync(insertReq);
        }
        catch { }

        usados.Add(tokenHash);
        await _storage.SetAsync(UsedTokensKey, usados);

        var licenciaLocal = new Licencia
        {
            Token = token,
            TokenHash = tokenHash,
            LicenciaTipo = tipo,
            FechaExpiracion = expiracion,
            FechaActivacion = DateTime.UtcNow,
            UltimaValidacion = DateTime.UtcNow,
            Valida = true,
            Mensaje = "Licencia activada en modo local (sin conexión al servidor)",
            PlanIncluido = plan,
            ModoGamificadoIncluido = game
        };

        await _storage.SetAsync(StorageKey, licenciaLocal);
        await _storage.SetAsync(LastValidatedKey, DateTime.UtcNow);
        return licenciaLocal;
    }

    private async Task<bool?> VerificarTokenEnSupabaseAsync(string tokenHash)
    {
        try
        {
            var url = $"{SupabaseConfig.Url}/rest/v1/used_tokens?token_hash=eq.{tokenHash}&select=token_hash";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("apikey", SupabaseConfig.AnonKey);
            request.Headers.Add("Authorization", $"Bearer {SupabaseConfig.AnonKey}");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;

            var body = await response.Content.ReadAsStringAsync();
            return body != "[]";
        }
        catch
        {
            return null;
        }
    }

    private bool ValidarFirmaECDSA(string token)
    {
        var partes = token.Split('|');
        if (partes.Length < 5)
            return false;

        if (partes[0] == "CDGv1")
            return ValidarHmacLocal(token);

        if (partes[0] != "CDGv2")
            return false;

        var contenidoBase = string.Join("|", partes.Take(partes.Length - 1));
        var firmaB64 = partes.Last();

        var data = Encoding.UTF8.GetBytes(contenidoBase);
        var firmaBytes = Convert.FromBase64String(
            firmaB64.Replace('-', '+').Replace('_', '/').PadRight(4 * ((firmaB64.Length + 3) / 4), '='));

        try
        {
            return GetVerifier().VerifyData(data, firmaBytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
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

    private bool ValidarHmacLocal(string token)
    {
        var partes = token.Split('|');
        if (partes.Length != 4 && partes.Length != 6)
            return false;

        var contenidoBase = string.Join("|", partes.Take(partes.Length - 1));
        var hmacEsperado = CalcularHmac(contenidoBase);
        return ConstanteTiempoIgualdad(partes.Last(), hmacEsperado);
    }

    private static string CalcularHmac(string contenido)
    {
        var rawKey = string.Concat("C", "D", "G", "HM", "AC", "2024", "ControlDe", "Gastos");
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        var contentBytes = Encoding.UTF8.GetBytes(contenido);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(contentBytes);
        return Convert.ToBase64String(hash).Replace('+', '-').Replace('/', '_').Replace("=", "");
    }

    private static bool ConstanteTiempoIgualdad(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var result = 0;
        for (var i = 0; i < a.Length; i++)
            result |= a[i] ^ b[i];
        return result == 0;
    }

    private static string CalcularSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task GuardarLicenciaLocalAsync(Licencia licencia)
    {
        await _storage.SetAsync(StorageKey, licencia);
    }

    public async Task<Licencia> ObtenerEstadoLicenciaAsync()
    {
        var licencia = await _storage.GetAsync<Licencia>(StorageKey);
        return licencia ?? new Licencia { Valida = false, Mensaje = "No hay licencia activada" };
    }

    public async Task<bool> VerificarYActualizarVigenciaAsync()
    {
        var licencia = await ObtenerEstadoLicenciaAsync();
        if (!licencia.Valida) return false;

        try
        {
            var response = await _http.PostAsJsonAsync("/api/license/validate", new { token = licencia.Token });
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ValidateResponse>();
                if (result != null && result.Valido)
                {
                    licencia.UltimaValidacion = DateTime.UtcNow;
                    await _storage.SetAsync(StorageKey, licencia);
                    await _storage.SetAsync(LastValidatedKey, DateTime.UtcNow);
                    return true;
                }

                licencia.Valida = false;
                licencia.Mensaje = result?.Mensaje ?? "Licencia inválida según el servidor";
                await _storage.SetAsync(StorageKey, licencia);
                return false;
            }
        }
        catch
        {
        }

        if (licencia.LicenciaTipo == TipoLicencia.ParaSiempre)
        {
            licencia.UltimaValidacion = DateTime.UtcNow;
            await _storage.SetAsync(StorageKey, licencia);
            return true;
        }

        var ultimaValidacion = await _storage.GetAsync<DateTime?>(LastValidatedKey);
        if (ultimaValidacion.HasValue && (DateTime.UtcNow - ultimaValidacion.Value).TotalDays < GracePeriodDays)
        {
            switch (licencia.LicenciaTipo)
            {
                case TipoLicencia.Trial:
                    if (licencia.FechaExpiracion == null || licencia.FechaExpiracion.Value < DateTime.UtcNow)
                    {
                        licencia.Valida = false;
                        licencia.Mensaje = "Licencia de prueba expirada";
                        await _storage.SetAsync(StorageKey, licencia);
                        return false;
                    }
                    licencia.UltimaValidacion = DateTime.UtcNow;
                    await _storage.SetAsync(StorageKey, licencia);
                    return true;
                default:
                    return false;
            }
        }

        licencia.Valida = false;
        licencia.Mensaje = "No se pudo validar la licencia. Conéctate a internet para continuar.";
        await _storage.SetAsync(StorageKey, licencia);
        return false;
    }

    public static (bool valido, TipoLicencia tipo, DateTime? expiracion, string mensaje, PlanType plan, bool modoGamificado) ValidarToken(string token)
    {
        var plan = PlanType.Local;
        var game = false;

        if (string.IsNullOrWhiteSpace(token))
            return (false, TipoLicencia.Trial, null, "Token vacío", plan, game);

        var partes = token.Split('|');
        if (partes.Length != 4 && partes.Length != 6)
            return (false, TipoLicencia.Trial, null, "Formato de token inválido", plan, game);

        if (partes[0] != "CDGv1" && partes[0] != "CDGv2")
            return (false, TipoLicencia.Trial, null, "Prefijo de token inválido", plan, game);

        var tipo = partes[1] switch
        {
            "TRIAL" => TipoLicencia.Trial,
            "FOREVER" => TipoLicencia.ParaSiempre,
            _ => TipoLicencia.Trial
        };

        if (partes.Length == 6)
        {
            plan = partes[3] switch
            {
                "NUBE" => PlanType.Nube,
                _ => PlanType.Local
            };
            game = partes[4] == "GAMEON";
        }

        if (tipo == TipoLicencia.ParaSiempre)
        {
            var ticksValidos = long.TryParse(partes[2], out var ticks) && ticks > 0;
            if (!ticksValidos)
                return (false, tipo, null, "Fecha de expiración inválida", plan, game);
            return (true, tipo, null, "Licencia de por vida activa (verificando firma...)", plan, game);
        }

        if (!long.TryParse(partes[2], out var expiryTicks))
            return (false, tipo, null, "Fecha de expiración inválida", plan, game);

        var expiracion = new DateTime(expiryTicks, DateTimeKind.Utc);

        if (expiracion < DateTime.UtcNow)
            return (false, tipo, expiracion, "Licencia expirada", plan, game);

        var diasRestantes = (int)(expiracion - DateTime.UtcNow).TotalDays;
        return (true, tipo, expiracion, $"Licencia válida por {diasRestantes} días (verificando firma...)", plan, game);
    }

    (bool valido, TipoLicencia tipo, DateTime? expiracion, string mensaje, PlanType plan, bool modoGamificado) ILicenciaService.ValidarToken(string token)
    {
        return ValidarToken(token);
    }
}

public class ValidateResponse
{
    public bool Valido { get; set; }
    public string Tipo { get; set; } = "";
    public long? ExpiracionTicks { get; set; }
    public string Mensaje { get; set; } = "";
    public string Plan { get; set; } = "";
    public bool ModoGamificado { get; set; }
}
