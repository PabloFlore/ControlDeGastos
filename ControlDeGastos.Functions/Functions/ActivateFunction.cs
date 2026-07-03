using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ControlDeGastos.Functions.Functions;

public class ActivateFunction
{
    readonly HttpClient _http;
    readonly ILogger _logger;

    public ActivateFunction(HttpClient http, ILoggerFactory loggerFactory)
    {
        _http = http;
        _logger = loggerFactory.CreateLogger<ActivateFunction>();
    }

    [Function("Activate")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "license/activate")] HttpRequestData req)
    {
        var requestBody = await req.ReadAsStringAsync();
        var activateReq = JsonSerializer.Deserialize<ValidateRequest>(requestBody ?? "{}");
        var token = activateReq?.Token ?? "";

        if (string.IsNullOrWhiteSpace(token))
        {
            var emptyResp = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await emptyResp.WriteAsJsonAsync(new ValidateResponse { Valido = false, Mensaje = "Token vacío" });
            return emptyResp;
        }

        var validation = TokenValidator.ValidateToken(token);
        if (!validation.Valido)
        {
            var invalidResp = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await invalidResp.WriteAsJsonAsync(validation);
            return invalidResp;
        }

        var supabaseUrl = Environment.GetEnvironmentVariable("Supabase__Url");
        var serviceRoleKey = Environment.GetEnvironmentVariable("Supabase__ServiceRoleKey");

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            var configResp = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await configResp.WriteAsJsonAsync(new ValidateResponse { Valido = false, Mensaje = "Servicio no configurado" });
            return configResp;
        }

        var tokenHash = TokenValidator.CalcularSha256Hex(token);

        try
        {
            var revoked = await CheckRevokedAsync(tokenHash, supabaseUrl, serviceRoleKey);
            if (revoked)
            {
                var revokedResp = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await revokedResp.WriteAsJsonAsync(new ValidateResponse { Valido = false, Mensaje = "Esta licencia ha sido revocada" });
                return revokedResp;
            }

            var alreadyUsed = await CheckUsedAsync(tokenHash, supabaseUrl, serviceRoleKey);
            if (alreadyUsed)
            {
                var usedResp = req.CreateResponse(System.Net.HttpStatusCode.OK);
                await usedResp.WriteAsJsonAsync(new ValidateResponse { Valido = false, Mensaje = "❌ Este token ya fue utilizado en otro dispositivo." });
                return usedResp;
            }

            await InsertUsedTokenAsync(tokenHash, validation, supabaseUrl, serviceRoleKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al activar token en Supabase");

            var errorResp = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await errorResp.WriteAsJsonAsync(new ValidateResponse { Valido = false, Mensaje = $"Error de conexión: {ex.Message}" });
            return errorResp;
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(validation);
        return response;
    }

    async Task<bool> CheckRevokedAsync(string tokenHash, string supabaseUrl, string serviceRoleKey)
    {
        var url = $"{supabaseUrl}/rest/v1/revoked_tokens?token_hash=eq.{tokenHash}&select=token_hash";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("apikey", serviceRoleKey);
        request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return false;

        var body = await response.Content.ReadAsStringAsync();
        return body != "[]";
    }

    async Task<bool> CheckUsedAsync(string tokenHash, string supabaseUrl, string serviceRoleKey)
    {
        var url = $"{supabaseUrl}/rest/v1/used_tokens?token_hash=eq.{tokenHash}&select=token_hash";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("apikey", serviceRoleKey);
        request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return false;

        var body = await response.Content.ReadAsStringAsync();
        return body != "[]";
    }

    async Task InsertUsedTokenAsync(string tokenHash, ValidateResponse validation, string supabaseUrl, string serviceRoleKey)
    {
        var row = new Dictionary<string, object>
        {
            ["token_hash"] = tokenHash,
            ["activated_at"] = DateTime.UtcNow.ToString("O"),
            ["token_type"] = validation.Tipo,
            ["plan"] = validation.Plan,
            ["game"] = validation.ModoGamificado
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{supabaseUrl}/rest/v1/used_tokens")
        {
            Content = JsonContent.Create(row)
        };
        request.Headers.Add("apikey", serviceRoleKey);
        request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");
        request.Headers.Add("Prefer", "return=minimal");

        await _http.SendAsync(request);
    }
}
