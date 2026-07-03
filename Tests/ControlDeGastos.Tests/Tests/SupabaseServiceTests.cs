using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq.Protected;
using Supabase;

namespace ControlDeGastos.Tests.Tests;

public class SupabaseServiceTests
{
    private readonly Mock<IStorageService> _storageMock = new();
    private readonly Mock<HttpMessageHandler> _httpHandlerMock = new();
    private readonly HttpClient _httpClient;

    public SupabaseServiceTests()
    {
        _httpClient = new HttpClient(_httpHandlerMock.Object);
    }

    private SupabaseService CrearService()
    {
        var service = new SupabaseService(_storageMock.Object, _httpClient, new Mock<ILogger<SupabaseService>>().Object);
        BypassClient(service);
        return service;
    }

    private static void BypassClient(SupabaseService service)
    {
        var field = typeof(SupabaseService).GetField("_client",
            BindingFlags.NonPublic | BindingFlags.Instance);
        var mockClient = new Mock<Client>(MockBehavior.Loose,
            "https://test.supabase.co", "test-anon-key",
            new SupabaseOptions());
        field!.SetValue(service, mockClient.Object);
    }

    private void SetupHttpResponse(HttpStatusCode code, string content)
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = code, Content = new StringContent(content) });
    }

    [Fact]
    public async Task ObtenerTodosAsync_RetornaLista()
    {
        var data = new List<Dictionary<string, object>>
        {
            new() { ["id"] = "1", ["nombre"] = "Test" }
        };
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        });
        SetupHttpResponse(HttpStatusCode.OK, json);

        var service = CrearService();
        var resultado = await service.ObtenerTodosAsync<Dictionary<string, object>>("test_table");

        Assert.NotNull(resultado);
        Assert.Single(resultado);
    }

    [Fact]
    public async Task ObtenerTodosAsync_ConFiltro_IncluyeFiltroEnUrl()
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.RequestUri!.ToString().Contains("id=eq.123")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("[]") });

        var service = CrearService();
        await service.ObtenerTodosAsync<object>("test_table", "id=eq.123");

        _httpHandlerMock.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("id=eq.123")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GuardarAsync_EnviaPost()
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.Method == HttpMethod.Post && r.RequestUri!.ToString().Contains("mi_tabla")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.Created, Content = new StringContent("[{\"nombre\":\"test\"}]") });

        var service = CrearService();
        var item = new Dictionary<string, object> { ["nombre"] = "test" };
        var resultado = await service.GuardarAsync("mi_tabla", item);

        Assert.NotNull(resultado);
    }

    [Fact]
    public async Task EliminarAsync_EnviaDelete()
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NoContent });

        var service = CrearService();
        await service.EliminarAsync<object>("mi_tabla", "123");

        _httpHandlerMock.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Delete),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task EliminarConFiltroAsync_EnviaDeleteConFiltro()
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.Method == HttpMethod.Delete &&
                    r.RequestUri!.ToString().Contains("campo=eq.valor")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.NoContent });

        var service = CrearService();
        await service.EliminarConFiltroAsync<object>("mi_tabla", "campo=eq.valor");

        _httpHandlerMock.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.Method == HttpMethod.Delete &&
                r.RequestUri!.ToString().Contains("campo=eq.valor")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarAsync_EnviaPatch()
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Patch),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("[{\"nombre\":\"actualizado\"}]") });

        var service = CrearService();
        var item = new Dictionary<string, object> { ["nombre"] = "actualizado" };
        var resultado = await service.ActualizarAsync("mi_tabla", "456", item);

        Assert.NotNull(resultado);
    }

    [Fact]
    public async Task API_HeadersIncluyenApikey()
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.Headers.Any(h => h.Key == "apikey")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("[]") });

        var service = CrearService();
        await service.ObtenerTodosAsync<object>("test");

        _httpHandlerMock.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.Headers.Any(h => h.Key == "apikey")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GuardarAsync_IncluyePreferHeader()
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.Headers.Any(h => h.Key == "Prefer" && h.Value.Contains("return=representation"))),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.Created, Content = new StringContent("[{}]") });

        var service = CrearService();
        await service.GuardarAsync("test", new Dictionary<string, object>());

        _httpHandlerMock.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.Headers.Any(h => h.Key == "Prefer")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ActualizarAsync_IncluyePreferHeader()
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.Is<HttpRequestMessage>(r =>
                    r.Headers.Any(h => h.Key == "Prefer" && h.Value.Contains("return=representation"))),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("[{}]") });

        var service = CrearService();
        await service.ActualizarAsync("test", "123", new Dictionary<string, object>());

        _httpHandlerMock.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.Headers.Any(h => h.Key == "Prefer")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task CerrarSesionAsync_NoRemueveStorageKey()
    {
        var service = CrearService();
        await service.CerrarSesionAsync();

        _storageMock.Verify(s => s.RemoveAsync("cdg_supabase_session"), Times.Never);
    }
}
