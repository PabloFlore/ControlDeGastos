using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

internal class PinData
{
    public string Hash { get; set; } = "";
    public string Salt { get; set; } = "";
}

public class PinLockService : IPinLockService
{
    private readonly IStorageService _storage;
    private readonly IUsuarioService _usuarioService;

    private bool _sesionAutenticada;

    private const string StorageKeyHash = "cdg_pin_hash";
    private const string StorageKeyIntentos = "cdg_pin_intentos";
    private const string StorageKeyBloqueoHasta = "cdg_pin_bloqueo_hasta";

    private const int MaxIntentos = 5;
    private const int TiempoEsperaSegundos = 30;
    private const int LongitudPin = 4;
    private const int IteracionesPbkdf2 = 1500;
    private const int TamanoClaveAes = 32;

    private const string RecoveryCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const string StorageKeyRecoveryCodeHash = "cdg_recovery_code_hash";

    public PinLockService(IStorageService storage, IUsuarioService usuarioService, ILogger<PinLockService>? logger = null)
    {
        _storage = storage;
        _usuarioService = usuarioService;
        _logger = logger;
    }

    public async Task<bool> EstaConfiguradoAsync()
    {
        return await _storage.KeyExistsAsync(StorageKeyHash);
    }

    public async Task ConfigurarPinAsync(string pin)
    {
        ValidarPin(pin);
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashPin(pin, salt);

        var data = new PinData
        {
            Hash = Convert.ToBase64String(hash),
            Salt = Convert.ToBase64String(salt)
        };

        var json = JsonSerializer.Serialize(data);
        await _storage.SetAsync(StorageKeyHash, json);
        await LimpiarIntentosAsync();

        await DerivarYCachearClaveAsync(pin, salt);
    }

    public async Task<bool> VerificarPinAsync(string pin)
    {
        var sw = Stopwatch.StartNew();
        _logger?.LogInformation("VerificarPinAsync: INICIO");

        var data = await ObtenerPinDataAsync();
        _logger?.LogInformation("VerificarPinAsync: ObtenerPinDataAsync {ms}ms", sw.ElapsedMilliseconds);
        if (data is null) return false;

        var hash = HashPin(pin, Convert.FromBase64String(data.Salt));
        var hashAlmacenado = Convert.FromBase64String(data.Hash);

        var coincide = CryptographicOperations.FixedTimeEquals(hash, hashAlmacenado);
        _logger?.LogInformation("VerificarPinAsync: Hash compare {ms}ms", sw.ElapsedMilliseconds);

        if (coincide)
        {
            await LimpiarIntentosAsync();
            _logger?.LogInformation("VerificarPinAsync: LimpiarIntentosAsync {ms}ms", sw.ElapsedMilliseconds);
            await DerivarYCachearClaveAsync(pin, Convert.FromBase64String(data.Salt));
            _logger?.LogInformation("VerificarPinAsync: DerivarYCachearClaveAsync {ms}ms", sw.ElapsedMilliseconds);
            await LimpiarClavesAntiguasCifradasAsync();
            _logger?.LogInformation("VerificarPinAsync: LimpiarClavesAntiguasCifradasAsync {ms}ms", sw.ElapsedMilliseconds);
        }
        else
        {
            await IncrementarIntentosAsync();
        }

        _logger?.LogInformation("VerificarPinAsync: TOTAL {ms}ms", sw.ElapsedMilliseconds);
        return coincide;
    }

    private readonly ILogger<PinLockService>? _logger;

    public async Task CambiarPinAsync(string pinViejo, string pinNuevo)
    {
        if (!await VerificarPinAsync(pinViejo))
            throw new InvalidOperationException("El PIN actual no es correcto.");

        await ConfigurarPinAsync(pinNuevo);
    }

    public async Task DesactivarPinAsync(string pin)
    {
        if (!await VerificarPinAsync(pin))
            throw new InvalidOperationException("El PIN no es correcto.");

        await _storage.RemoveAsync(StorageKeyHash);
        await LimpiarIntentosAsync();
        CerrarSesion();
    }

    public async Task<string?> GenerarRecoveryCodeSiNoExisteAsync()
    {
        if (await _storage.KeyExistsAsync(StorageKeyRecoveryCodeHash))
            return null;

        var chars = RecoveryCodeChars.ToCharArray();
        var random = RandomNumberGenerator.GetBytes(8);
        var parte1 = new string([chars[random[0] % chars.Length], chars[random[1] % chars.Length], chars[random[2] % chars.Length], chars[random[3] % chars.Length]]);
        var parte2 = new string([chars[random[4] % chars.Length], chars[random[5] % chars.Length], chars[random[6] % chars.Length], chars[random[7] % chars.Length]]);
        var code = $"{parte1}-{parte2}";

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        await _storage.SetAsync(StorageKeyRecoveryCodeHash, Convert.ToBase64String(hash));

        return code;
    }

    public async Task<bool> VerificarRecoveryCodeAsync(string code)
    {
        var storedHash = await _storage.GetAsync<string>(StorageKeyRecoveryCodeHash);
        if (string.IsNullOrEmpty(storedHash)) return false;

        var normalized = code
            .Trim()
            .Replace("CDG-", "", StringComparison.OrdinalIgnoreCase)
            .Replace("-", "")
            .ToUpperInvariant();

        if (normalized.Length == 8)
            normalized = $"{normalized[..4]}-{normalized[4..]}";
        else if (normalized.Length != 9 || normalized[4] != '-')
            return false;

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var storedBytes = Convert.FromBase64String(storedHash);

        return CryptographicOperations.FixedTimeEquals(hashBytes, storedBytes);
    }

    public async Task DesactivarConRecoveryCodeAsync(string code)
    {
        if (!await VerificarRecoveryCodeAsync(code))
            throw new InvalidOperationException("El código de recuperación no es correcto.");

        await _storage.RemoveAsync(StorageKeyHash);
        await LimpiarIntentosAsync();
        CerrarSesion();
    }

    public async Task<int> ObtenerDelayBloqueoSegundosAsync()
    {
        try
        {
            var usuario = await _usuarioService.ObtenerUsuarioAsync();
            return usuario?.PinDelaySegundos ?? 30;
        }
        catch
        {
            return 30;
        }
    }

    public async Task GuardarDelayBloqueoSegundosAsync(int segundos)
    {
        var delayValido = segundos switch
        {
            0 => 0,
            30 => 30,
            60 => 60,
            300 => 300,
            _ => 30
        };

        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null) return;
        usuario.PinDelaySegundos = delayValido;
        await _usuarioService.GuardarUsuarioAsync(usuario);
    }

    public bool SesionEstaAutenticada() => _sesionAutenticada;

    public void EstablecerSesionAutenticada() => _sesionAutenticada = true;

    public void CerrarSesion()
    {
        _sesionAutenticada = false;
        CipherKeyStore.ClearKey();
    }

    public async Task<int> ObtenerIntentosFallidosAsync()
    {
        var intentos = await _storage.GetAsync<int>(StorageKeyIntentos);
        return intentos;
    }

    public async Task<bool> EstaTemporalmenteBloqueadoAsync()
    {
        var bloqueoHasta = await _storage.GetAsync<string>(StorageKeyBloqueoHasta);
        if (string.IsNullOrEmpty(bloqueoHasta)) return false;

        if (long.TryParse(bloqueoHasta, out var ticks))
        {
            var hasta = new DateTime(ticks, DateTimeKind.Utc);
            return DateTime.UtcNow < hasta;
        }

        return false;
    }

    public async Task<int> ObtenerTiempoEsperaRestanteSegundosAsync()
    {
        var bloqueoHasta = await _storage.GetAsync<string>(StorageKeyBloqueoHasta);
        if (string.IsNullOrEmpty(bloqueoHasta)) return 0;

        if (long.TryParse(bloqueoHasta, out var ticks))
        {
            var hasta = new DateTime(ticks, DateTimeKind.Utc);
            var restante = (int)(hasta - DateTime.UtcNow).TotalSeconds;
            return Math.Max(0, restante);
        }

        return 0;
    }

    private static void ValidarPin(string pin)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length != LongitudPin || !pin.All(char.IsDigit))
            throw new ArgumentException($"El PIN debe tener exactamente {LongitudPin} dígitos numéricos.");
    }

    private static byte[] HashPin(string pin, byte[] salt)
    {
        var input = new byte[salt.Length + pin.Length];
        Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
        Encoding.UTF8.GetBytes(pin, 0, pin.Length, input, salt.Length);
        var hash = SHA256.HashData(input);
        Array.Clear(input, 0, input.Length);
        return hash;
    }

    private async Task DerivarYCachearClaveAsync(string pin, byte[] salt)
    {
        var sw = Stopwatch.StartNew();
        _logger?.LogInformation("DerivarYCachearClaveAsync: INICIO PBKDF2 {Iter} iteraciones", IteracionesPbkdf2);
        var pinBytes = Encoding.UTF8.GetBytes(pin);
        try
        {
            var key = Rfc2898DeriveBytes.Pbkdf2(pinBytes, salt, IteracionesPbkdf2, HashAlgorithmName.SHA256, TamanoClaveAes);
            CipherKeyStore.SetKey(key);
            _logger?.LogInformation("DerivarYCachearClaveAsync: PBKDF2 completado {ms}ms", sw.ElapsedMilliseconds);
        }
        finally
        {
            Array.Clear(pinBytes, 0, pinBytes.Length);
        }
    }

    private async Task<PinData?> ObtenerPinDataAsync()
    {
        var json = await _storage.GetAsync<string>(StorageKeyHash);
        if (string.IsNullOrEmpty(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<PinData>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task IncrementarIntentosAsync()
    {
        var intentos = await ObtenerIntentosFallidosAsync();
        intentos++;
        await _storage.SetAsync(StorageKeyIntentos, intentos);

        if (intentos >= MaxIntentos)
        {
            var bloqueoHasta = DateTime.UtcNow.AddSeconds(TiempoEsperaSegundos);
            await _storage.SetAsync(StorageKeyBloqueoHasta, bloqueoHasta.Ticks.ToString());
        }
    }

    private async Task LimpiarIntentosAsync()
    {
        await _storage.RemoveAsync(StorageKeyIntentos);
        await _storage.RemoveAsync(StorageKeyBloqueoHasta);
    }

    private async Task LimpiarClavesAntiguasCifradasAsync()
    {
        try
        {
            await _storage.RemoveAsync("cdg_used_tokens");
        }
        catch
        {
        }
    }
}
