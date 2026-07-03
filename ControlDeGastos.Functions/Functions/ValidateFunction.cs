using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ControlDeGastos.Functions.Functions;

public class ValidateFunction
{
    readonly HttpClient _http;
    readonly ILogger _logger;

    public ValidateFunction(HttpClient http, ILoggerFactory loggerFactory)
    {
        _http = http;
        _logger = loggerFactory.CreateLogger<ValidateFunction>();
    }

    [Function("Validate")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "api/license/validate")] HttpRequestData req)
    {
        var requestBody = await req.ReadAsStringAsync();
        var validateReq = JsonSerializer.Deserialize<ValidateRequest>(requestBody ?? "{}");
        var token = validateReq?.Token ?? "";

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

        var tokenHash = TokenValidator.CalcularSha256Hex(token);

        try
        {
            var revoked = await CheckRevokedAsync(tokenHash);
            if (revoked)
            {
                validation.Valido = false;
                validation.Mensaje = "Esta licencia ha sido revocada";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al verificar revocación en Supabase");
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteAsJsonAsync(validation);
        return response;
    }

    async Task<bool> CheckRevokedAsync(string tokenHash)
    {
        var supabaseUrl = Environment.GetEnvironmentVariable("Supabase__Url");
        var serviceRoleKey = Environment.GetEnvironmentVariable("Supabase__ServiceRoleKey");

        if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(serviceRoleKey))
            return false;

        var url = $"{supabaseUrl}/rest/v1/revoked_tokens?token_hash=eq.{tokenHash}&select=token_hash";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("apikey", serviceRoleKey);
        request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return false;

        var body = await response.Content.ReadAsStringAsync();
        return body != "[]";
    }
}
