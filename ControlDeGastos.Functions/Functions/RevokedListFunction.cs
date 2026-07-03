using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace ControlDeGastos.Functions.Functions;

public class RevokedListFunction
{
    readonly HttpClient _http;
    readonly ILogger _logger;

    public RevokedListFunction(HttpClient http, ILoggerFactory loggerFactory)
    {
        _http = http;
        _logger = loggerFactory.CreateLogger<RevokedListFunction>();
    }

    [Function("RevokedList")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "api/license/revoked")] HttpRequestData req)
    {
        var apiKey = req.Headers.TryGetValues("X-Api-Key", out var values) ? values.FirstOrDefault() : null;
        var expectedKey = Environment.GetEnvironmentVariable("Revocation__ApiKey");

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey != expectedKey)
        {
            var unauthResp = req.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
            await unauthResp.WriteAsJsonAsync(new { error = "No autorizado" });
            return unauthResp;
        }

        var supabaseUrl = Environment.GetEnvironmentVariable("Supabase__Url");
        var serviceRoleKey = Environment.GetEnvironmentVariable("Supabase__ServiceRoleKey");

        try
        {
            var url = $"{supabaseUrl}/rest/v1/revoked_tokens?select=token_hash,revoked_at,reason&order=revoked_at.desc";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("apikey", serviceRoleKey);
            request.Headers.Add("Authorization", $"Bearer {serviceRoleKey}");

            var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            var okResp = req.CreateResponse(System.Net.HttpStatusCode.OK);
            okResp.Headers.Add("Content-Type", "application/json");
            await okResp.WriteStringAsync(body);
            return okResp;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al listar tokens revocados");

            var errResp = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await errResp.WriteAsJsonAsync(new { error = $"Error de conexión: {ex.Message}" });
            return errResp;
        }
    }
}
