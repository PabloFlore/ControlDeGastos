using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using static ControlDeGastos.Services.LicenciaService;

namespace ControlDeGastos.Tests.Tests;

public class LicenciaServiceTests
{
    private static readonly byte[] TestPublicKeyBytes = new byte[] { 48, 89, 48, 19, 6, 7, 42, 134, 72, 206, 61, 2, 1, 6, 8, 42, 134, 72, 206, 61, 3, 1, 7, 3, 66, 0, 4, 45, 85, 9, 191, 65, 250, 60, 109, 6, 28, 92, 118, 29, 115, 120, 180, 222, 98, 69, 153, 190, 35, 114, 26, 187, 229, 200, 93, 96, 108, 141, 160, 42, 215, 192, 186, 4, 77, 215, 122, 96, 53, 228, 108, 169, 44, 239, 203, 200, 246, 106, 0, 158, 21, 135, 31, 245, 3, 48, 149, 105, 61, 61, 164 };

    private static LicenciaService CrearService(IStorageService storage)
    {
        var http = new HttpClient(new MockHttpHandler());
        return new LicenciaService(storage, new Mock<ILogger<LicenciaService>>().Object, TestPublicKeyBytes, http);
    }

    private class MockHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("No hay servidor disponible (simulado)");
        }
    }

    private static string GenerarTokenV2(string tipo, int dias, string plan = "LOCAL", bool game = false)
    {
        var expiryTicks = tipo == "FOREVER"
            ? DateTime.UtcNow.Ticks.ToString()
            : DateTime.UtcNow.AddDays(dias).Ticks.ToString();

        var planStr = plan == "NUBE" ? "NUBE" : "LOCAL";
        var gameStr = game ? "GAMEON" : "GAMEOFF";
        var contenido = $"CDGv2|{tipo}|{expiryTicks}|{planStr}|{gameStr}";

        const string privateKeyPem =
            "-----BEGIN EC PRIVATE KEY-----\n" +
            "MHcCAQEEIMDQF+ulW7UJ+jnePLB/Psmga/N09fQPadwIhzpDf48NoAoGCCqGSM49\n" +
            "AwEHoUQDQgAEpRRYL7A3am4YA/WKBKvLcQvQON1QVoH9gmUiOi5QI7YEY114BlxI\n" +
            "yb+s9dJ6J/8LTMcDoKO1EhqJcH8EOzNJHw==\n" +
            "-----END EC PRIVATE KEY-----";

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem);
        var data = Encoding.UTF8.GetBytes(contenido);
        var sig = ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        var sigB64 = Convert.ToBase64String(sig).Replace('+', '-').Replace('/', '_').Replace("=", "");

        return $"{contenido}|{sigB64}";
    }

    [Fact]
    public void ValidarToken_TokenVacio_RetornaInvalido()
    {
        var (valido, _, _, mensaje, _, _) = ValidarToken("");
        Assert.False(valido);
        Assert.Contains("vacío", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarToken_TokenConFormatoInvalido_RetornaInvalido()
    {
        var (valido, _, _, mensaje, _, _) = ValidarToken("INVALIDO|SIN|PIPES");
        Assert.False(valido);
        Assert.Contains("formato", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarToken_PrefijoInvalido_RetornaInvalido()
    {
        var (valido, _, _, mensaje, _, _) = ValidarToken("BADv1|TRIAL|123|hmac");
        Assert.False(valido);
        Assert.Contains("prefijo", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarToken_SinHmac_RetornaValidoPorFormato()
    {
        var (valido, _, _, mensaje, _, _) = ValidarToken("CDGv1|TRIAL|999999999999999999|firma_invalida");
        Assert.True(valido);
        Assert.Contains("verificando firma", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarToken_CDGv2Valido_RetornaValido()
    {
        var (valido, _, _, mensaje, _, _) = ValidarToken("CDGv2|TRIAL|999999999999999999|firma_invalida");
        Assert.True(valido);
        Assert.Contains("verificando firma", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarToken_ParaSiempreConFormatoValido_RetornaValido()
    {
        var (valido, tipo, expiracion, mensaje, plan, game) = ValidarToken("CDGv1|FOREVER|1234567890|hmac");
        Assert.True(valido);
        Assert.Equal(TipoLicencia.ParaSiempre, tipo);
        Assert.Null(expiracion);
        Assert.Equal(PlanType.Local, plan);
        Assert.False(game);
    }

    [Fact]
    public void ValidarToken_ParaSiempreConPlanNubeYGameOn_RetornaValidoConFlags()
    {
        var (valido, tipo, _, _, plan, game) = ValidarToken("CDGv1|FOREVER|1234567890|NUBE|GAMEON|hmac");
        Assert.True(valido);
        Assert.Equal(TipoLicencia.ParaSiempre, tipo);
        Assert.Equal(PlanType.Nube, plan);
        Assert.True(game);
    }

    [Fact]
    public void ValidarToken_TrialExpirado_RetornaInvalido()
    {
        var ticksExpirado = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var (valido, _, _, mensaje, _, _) = ValidarToken($"CDGv1|TRIAL|{ticksExpirado}|hmac");
        Assert.False(valido);
        Assert.Contains("expirada", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarToken_TrialValido_RetornaValido()
    {
        var ticksFuturo = DateTime.UtcNow.AddDays(30).Ticks;
        var (valido, tipo, expiracion, _, _, _) = ValidarToken($"CDGv1|TRIAL|{ticksFuturo}|hmac");
        Assert.True(valido);
        Assert.Equal(TipoLicencia.Trial, tipo);
        Assert.NotNull(expiracion);
        Assert.True(expiracion > DateTime.UtcNow);
    }

    [Fact]
    public void ValidarToken_TicksInvalidos_RetornaInvalido()
    {
        var (valido, _, _, mensaje, _, _) = ValidarToken("CDGv1|TRIAL|no_son_ticks|hmac");
        Assert.False(valido);
        Assert.Contains("fecha", mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActivarLicenciaAsync_TokenYaUsado_RetornaInvalido()
    {
        var storage = new InMemoryStorageService();
        var service = CrearService(storage);

        var token = GenerarTokenV2("TRIAL", 30);

        var primero = await service.ActivarLicenciaAsync(token);
        var resultado = await service.ActivarLicenciaAsync(token);

        Assert.False(resultado.Valida);
        Assert.Contains("ya fue utilizado", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ObtenerEstadoLicenciaAsync_SinLicencia_RetornaInvalida()
    {
        var storage = new InMemoryStorageService();
        var service = CrearService(storage);

        var estado = await service.ObtenerEstadoLicenciaAsync();
        Assert.False(estado.Valida);
    }

    [Fact]
    public async Task VerificarYActualizarVigenciaAsync_SinLicencia_RetornaFalse()
    {
        var storage = new InMemoryStorageService();
        var service = CrearService(storage);

        var resultado = await service.VerificarYActualizarVigenciaAsync();
        Assert.False(resultado);
    }

    [Fact]
    public async Task VerificarYActualizarVigenciaAsync_LicenciaInvalida_RetornaFalse()
    {
        var storage = new InMemoryStorageService();
        var service = CrearService(storage);
        await storage.SetAsync("cdg_licencia", new Licencia { Valida = false, LicenciaTipo = TipoLicencia.Trial });

        var resultado = await service.VerificarYActualizarVigenciaAsync();
        Assert.False(resultado);
    }

    [Fact]
    public async Task VerificarYActualizarVigenciaAsync_ParaSiempre_RetornaTrue()
    {
        var storage = new InMemoryStorageService();
        var service = CrearService(storage);
        await storage.SetAsync("cdg_licencia", new Licencia { Valida = true, LicenciaTipo = TipoLicencia.ParaSiempre });
        await storage.SetAsync<DateTime?>("cdg_last_validated", DateTime.UtcNow);

        var resultado = await service.VerificarYActualizarVigenciaAsync();
        Assert.True(resultado);
    }

    [Fact]
    public async Task VerificarYActualizarVigenciaAsync_TrialValido_RetornaTrue()
    {
        var storage = new InMemoryStorageService();
        var service = CrearService(storage);
        await storage.SetAsync("cdg_licencia", new Licencia
        {
            Valida = true,
            LicenciaTipo = TipoLicencia.Trial,
            FechaExpiracion = DateTime.UtcNow.AddDays(30),
        });
        await storage.SetAsync<DateTime?>("cdg_last_validated", DateTime.UtcNow);

        var resultado = await service.VerificarYActualizarVigenciaAsync();
        Assert.True(resultado);
    }

    [Fact]
    public async Task VerificarYActualizarVigenciaAsync_TrialExpirado_RetornaFalse()
    {
        var storage = new InMemoryStorageService();
        var service = CrearService(storage);
        await storage.SetAsync("cdg_licencia", new Licencia
        {
            Valida = true,
            LicenciaTipo = TipoLicencia.Trial,
            FechaExpiracion = DateTime.UtcNow.AddDays(-1),
        });
        await storage.SetAsync<DateTime?>("cdg_last_validated", DateTime.UtcNow);

        var resultado = await service.VerificarYActualizarVigenciaAsync();
        Assert.False(resultado);

        var estado = await service.ObtenerEstadoLicenciaAsync();
        Assert.False(estado.Valida);
        Assert.Contains("expirada", estado.Mensaje);
    }

    [Fact]
    public async Task VerificarYActualizarVigenciaAsync_TrialSinFechaExpiracion_RetornaFalse()
    {
        var storage = new InMemoryStorageService();
        var service = CrearService(storage);
        await storage.SetAsync("cdg_licencia", new Licencia
        {
            Valida = true,
            LicenciaTipo = TipoLicencia.Trial,
            FechaExpiracion = null,
        });
        await storage.SetAsync<DateTime?>("cdg_last_validated", DateTime.UtcNow);

        var resultado = await service.VerificarYActualizarVigenciaAsync();
        Assert.False(resultado);
    }

    [Fact]
    public async Task VerificarYActualizarVigenciaAsync_ParaSiempre_ActualizaUltimaValidacion()
    {
        var storage = new InMemoryStorageService();
        var service = CrearService(storage);
        var licencia = new Licencia { Valida = true, LicenciaTipo = TipoLicencia.ParaSiempre, UltimaValidacion = DateTime.UtcNow.AddDays(-5) };
        await storage.SetAsync("cdg_licencia", licencia);
        await storage.SetAsync<DateTime?>("cdg_last_validated", DateTime.UtcNow);

        await service.VerificarYActualizarVigenciaAsync();

        var actualizada = await storage.GetAsync<Licencia>("cdg_licencia");
        Assert.NotNull(actualizada);
        Assert.True(actualizada!.UltimaValidacion > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task ActivarLicenciaAsync_PlanNube_ApiCaida_RetornaInvalido()
    {
        var storage = new InMemoryStorageService();
        var service = CrearService(storage);

        var token = GenerarTokenV2("TRIAL", 30, "NUBE", true);

        var resultado = await service.ActivarLicenciaAsync(token);

        Assert.False(resultado.Valida);
        Assert.Contains("conexión", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ActivarLicenciaAsync_PlanLocal_ApiCaida_FallbackLocal()
    {
        var storage = new InMemoryStorageService();
        var service = CrearService(storage);

        var token = GenerarTokenV2("TRIAL", 30);

        var resultado = await service.ActivarLicenciaAsync(token);

        Assert.True(resultado.Valida);
        Assert.Contains("modo local", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerificarYActualizarVigenciaAsync_TipoDesconocido_RetornaFalse()
    {
        var storage = new InMemoryStorageService();
        var service = CrearService(storage);
        await storage.SetAsync("cdg_licencia", new Licencia
        {
            Valida = true,
            LicenciaTipo = (TipoLicencia)999,
        });
        await storage.SetAsync<DateTime?>("cdg_last_validated", DateTime.UtcNow);

        var resultado = await service.VerificarYActualizarVigenciaAsync();
        Assert.False(resultado);
    }

    [Fact]
    public async Task GuardarLicenciaLocalAsync_PersisteEnStorage()
    {
        var storage = new InMemoryStorageService();
        var service = CrearService(storage);

        var licencia = new Licencia
        {
            Token = "test-token",
            TokenHash = "hash123",
            LicenciaTipo = TipoLicencia.ParaSiempre,
            Valida = true,
        };

        await service.GuardarLicenciaLocalAsync(licencia);

        var guardada = await storage.GetAsync<Licencia>("cdg_licencia");
        Assert.NotNull(guardada);
        Assert.Equal("test-token", guardada!.Token);
        Assert.True(guardada.Valida);
        Assert.Equal(TipoLicencia.ParaSiempre, guardada.LicenciaTipo);
    }
}
