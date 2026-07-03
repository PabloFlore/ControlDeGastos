using System.Security.Cryptography;
using System.Text;

namespace ControlDeGastos.Functions;

public static class TokenValidator
{
    private static readonly byte[] PublicKeyBytes =
    [
        48, 89, 48, 19, 6, 7, 42, 134, 72, 206, 61, 2, 1, 6, 8, 42, 134, 72, 206, 61, 3, 1, 7, 3, 66, 0, 4,
        228, 32, 164, 132, 157, 64, 233, 225, 242, 89, 10, 191, 20, 113, 221, 241, 196, 55, 220, 43, 181, 219,
        225, 111, 115, 119, 210, 100, 129, 129, 7, 101, 0, 100, 225, 3, 183, 2, 225, 117, 130, 31, 13, 6, 192,
        194, 76, 237, 29, 136, 206, 247, 124, 198, 58, 216, 240, 4, 123, 34, 155, 91, 27, 232
    ];

    public static ValidateResponse ValidateToken(string token)
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

    public static bool ValidarFirma(string token, string contenidoBase, string firma, string prefix, byte[] PublicKeyBytes)
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

    public static string CalcularSha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
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
}
