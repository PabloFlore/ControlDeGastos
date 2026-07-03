using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ControlDeGastos.Functions.Functions;

public class RevokeFunction
{
    readonly HttpClient _http;
    readonly ILogger _logger;

    public RevokeFunction(HttpClient http, ILoggerFactory loggerFactory)
    {
        _http = http;
        _logger = loggerFactory.CreateLogger<RevokeFunction>();
    }

    [Function("Revoke")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "license/revoke")] HttpRequestData req)
    {
        var apiKey = req.Headers.TryGetValues("X-Api-Key", out var values) ? values.FirstOrDefault() : null;
        var expectedKey = Environment.GetEnvironmentVariable("Revocation__ApiKey");

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey != expectedKey)
        {
            var unauthResp = req.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
            await unauthResp.WriteAsJsonAsync(new { error = "No autorizado" });
            return unauthResp;
        }

        var requestBody = await req.ReadAsStringAsync();
        var revokeReq = JsonSerializer.Deserialize<RevokeRequest>(requestBody ?? "{}");

        if (string.IsNullOrWhiteSpace(revokeReq?.TokenHash))
        {
            var badResp = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badResp.WriteAsJsonAsync(new { error = "token_hash es requerido" });
            return badResp;
        }

        var supabaseUrl = Environment.GetEnvironmentVariable("Supabase__Url");
        var serviceRoleKey = Environment.GetEnvironmentVariable("Supabase__ServiceRoleKey");

        try
        {
            var row = new Dictionary<string, object>
            {
                ["token_hash"] = revokeReq.TokenHash,
                ["revoked_at"] = DateTime.UtcNow.ToString("O"),
                ["reason"] = revokeReq.Reason ?? ""
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{supabaseUrl}/rest/v1/revoked_tokens")
            {
                Content = JsonContent.Create(row)
            };
            request.Headers.Add("apikey", serviceRoleKey);
            request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");
            request.Headers.Add("Prefer", "return=minimal");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errResp = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
                await errResp.WriteAsJsonAsync(new { error = "No se pudo revocar el token" });
                return errResp;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al revocar token");

            var errResp = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errResp.WriteAsJsonAsync(new { error = $"Error de conexión: {ex.Message}" });
            return errResp;
        }

        var okResp = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await okResp.WriteAsJsonAsync(new { status = "revoked", token_hash = revokeReq.TokenHash });
        return okResp;
    }
}
