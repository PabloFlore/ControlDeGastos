namespace ControlDeGastos.Tests.Tests;

public class OnboardingServiceTests
{
    private readonly InMemoryStorageService _storage = new();

    public OnboardingServiceTests()
    {
        _storage.ClearAsync().GetAwaiter().GetResult();
    }

    private OnboardingService CrearService() => new(_storage);

    [Fact]
    public async Task EstaCompletadoAsync_SinData_RetornaFalse()
    {
        var service = CrearService();
        var completado = await service.EstaCompletadoAsync();
        Assert.False(completado);
    }

    [Fact]
    public async Task CompletarAsync_PersisteFlag()
    {
        var service = CrearService();
        await service.CompletarAsync();

        Assert.True(await _storage.KeyExistsAsync("cdg_onboarding_completado"));
        var val = await _storage.GetAsync<bool>("cdg_onboarding_completado");
        Assert.True(val);
    }

    [Fact]
    public async Task CompletarAsync_EstaCompletadoRetornaTrue()
    {
        var service = CrearService();
        await service.CompletarAsync();

        var completado = await service.EstaCompletadoAsync();
        Assert.True(completado);
    }

    [Fact]
    public async Task SaltarAsync_PersisteFlag()
    {
        var service = CrearService();
        await service.SaltarAsync();

        Assert.True(await _storage.KeyExistsAsync("cdg_onboarding_completado"));
    }

    [Fact]
    public async Task SaltarAsync_EstaCompletadoRetornaTrue()
    {
        var service = CrearService();
        await service.SaltarAsync();

        var completado = await service.EstaCompletadoAsync();
        Assert.True(completado);
    }

    [Fact]
    public async Task CompletarYCrearNuevoService_SiguePersistido()
    {
        var service1 = CrearService();
        await service1.CompletarAsync();

        var service2 = CrearService();
        Assert.True(await service2.EstaCompletadoAsync());
    }
}
