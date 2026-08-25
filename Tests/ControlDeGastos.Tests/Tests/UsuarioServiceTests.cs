using Moq;

namespace ControlDeGastos.Tests.Tests;

public class UsuarioServiceTests
{
    private readonly InMemoryStorageService _storage = new();
    private readonly Mock<ISupabaseService> _supabaseMock = new();

    public UsuarioServiceTests()
    {
        _storage.ClearAsync().GetAwaiter().GetResult();
    }

    private UsuarioService CrearService() => new(_storage, _supabaseMock.Object);

    [Fact]
    public async Task ObtenerUsuarioAsync_PrimeraVez_CreaUsuarioDefault()
    {
        var service = CrearService();

        var usuario = await service.ObtenerUsuarioAsync();

        Assert.NotNull(usuario);
        Assert.Equal("Usuario", usuario.Nombre);
        Assert.Equal(PlanType.Local, usuario.PlanActivo);
        Assert.Equal("MXN", usuario.Moneda);
        Assert.False(usuario.ModoGamificadoActivo);
        Assert.NotEqual(Guid.Empty, usuario.Id);
    }

    [Fact]
    public async Task ObtenerUsuarioAsync_PrimeraVez_PersisteEnStorage()
    {
        var service = CrearService();
        var usuario = await service.ObtenerUsuarioAsync();

        var guardado = await _storage.GetAsync<Usuario>("cdg_usuario");
        Assert.NotNull(guardado);
        Assert.Equal(usuario.Id, guardado!.Id);
    }

    [Fact]
    public async Task ObtenerUsuarioAsync_YaExiste_NoSobrescribe()
    {
        var preexistente = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Predefinido",
            PlanActivo = PlanType.Nube,
            Moneda = "USD"
        };
        await _storage.SetAsync("cdg_usuario", preexistente);

        var service = CrearService();
        var usuario = await service.ObtenerUsuarioAsync();

        Assert.Equal(preexistente.Id, usuario.Id);
        Assert.Equal("Predefinido", usuario.Nombre);
        Assert.Equal(PlanType.Nube, usuario.PlanActivo);
        Assert.Equal("USD", usuario.Moneda);
    }

    [Fact]
    public async Task GuardarUsuarioAsync_ActualizaStorage()
    {
        var service = CrearService();
        var usuario = new Usuario { Id = Guid.NewGuid(), Nombre = "Actualizado" };

        await service.GuardarUsuarioAsync(usuario);

        var guardado = await _storage.GetAsync<Usuario>("cdg_usuario");
        Assert.NotNull(guardado);
        Assert.Equal("Actualizado", guardado!.Nombre);
    }

    [Fact]
    public async Task CambiarPlanAsync_ModificaPlan()
    {
        var service = CrearService();
        var original = await service.ObtenerUsuarioAsync();
        Assert.Equal(PlanType.Local, original.PlanActivo);

        await service.CambiarPlanAsync(PlanType.Nube);

        var actualizado = await service.ObtenerUsuarioAsync();
        Assert.Equal(PlanType.Nube, actualizado.PlanActivo);
    }

    [Fact]
    public async Task CambiarModoGamificadoAsync_AlternaFlag()
    {
        var service = CrearService();
        var original = await service.ObtenerUsuarioAsync();
        Assert.False(original.ModoGamificadoActivo);

        await service.CambiarModoGamificadoAsync(true);

        var actualizado = await service.ObtenerUsuarioAsync();
        Assert.True(actualizado.ModoGamificadoActivo);
    }

    [Fact]
    public async Task CambiarMostrarGraficaIngresosAsync_AlternaFlag()
    {
        var service = CrearService();
        var original = await service.ObtenerUsuarioAsync();
        Assert.False(original.MostrarGraficaIngresos);

        await service.CambiarMostrarGraficaIngresosAsync(true);

        var actualizado = await service.ObtenerUsuarioAsync();
        Assert.True(actualizado.MostrarGraficaIngresos);
    }
}
