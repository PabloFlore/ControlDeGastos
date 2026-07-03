using Moq;

namespace ControlDeGastos.Tests.Tests;

public class PinLockServiceTests
{
    private readonly InMemoryStorageService _storage = new();
    private readonly Mock<IUsuarioService> _usuarioServiceMock = new();

    public PinLockServiceTests()
    {
        _storage.ClearAsync().GetAwaiter().GetResult();
    }

    private PinLockService CrearService()
    {
        _usuarioServiceMock.Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), Nombre = "Test" });
        _usuarioServiceMock.Setup(s => s.GuardarUsuarioAsync(It.IsAny<Usuario>()))
            .Returns(Task.CompletedTask);

        return new PinLockService(_storage, _usuarioServiceMock.Object);
    }

    private PinLockService CrearServiceConStorage(IStorageService storage, Mock<IUsuarioService> mock)
    {
        return new PinLockService(storage, mock.Object);
    }

    [Fact]
    public async Task EstaConfiguradoAsync_SinPin_DevuelveFalse()
    {
        var service = CrearService();
        Assert.False(await service.EstaConfiguradoAsync());
    }

    [Fact]
    public async Task ConfigurarPinAsync_PinValido_GuardaHash()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");

        Assert.True(await service.EstaConfiguradoAsync());
    }

    [Fact]
    public async Task ConfigurarPinAsync_PinInvalido_LanzaExcepcion()
    {
        var service = CrearService();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ConfigurarPinAsync("123"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ConfigurarPinAsync("12345"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ConfigurarPinAsync("abcd"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ConfigurarPinAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ConfigurarPinAsync(null!));
    }

    [Fact]
    public async Task VerificarPinAsync_PinCorrecto_DevuelveTrue()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");

        Assert.True(await service.VerificarPinAsync("1234"));
    }

    [Fact]
    public async Task VerificarPinAsync_PinIncorrecto_DevuelveFalse()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");

        Assert.False(await service.VerificarPinAsync("5678"));
    }

    [Fact]
    public async Task VerificarPinAsync_MultiplesFallos_CuentaIntentos()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");

        await service.VerificarPinAsync("0000");
        await service.VerificarPinAsync("0000");
        await service.VerificarPinAsync("0000");

        var intentos = await service.ObtenerIntentosFallidosAsync();
        Assert.Equal(3, intentos);
    }

    [Fact]
    public async Task VerificarPinAsync_CincoFallos_BloqueaTemporalmente()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");

        for (int i = 0; i < 5; i++)
        {
            await service.VerificarPinAsync("0000");
        }

        Assert.True(await service.EstaTemporalmenteBloqueadoAsync());
    }

    [Fact]
    public async Task VerificarPinAsync_AciertoLuegoDeFallos_ReiniciaIntentos()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");

        await service.VerificarPinAsync("0000");
        await service.VerificarPinAsync("0000");
        await service.VerificarPinAsync("1234");

        var intentos = await service.ObtenerIntentosFallidosAsync();
        Assert.Equal(0, intentos);
    }

    [Fact]
    public async Task CambiarPinAsync_PinCorrecto_CambiaHash()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");

        await service.CambiarPinAsync("1234", "5678");

        Assert.True(await service.VerificarPinAsync("5678"));
        Assert.False(await service.VerificarPinAsync("1234"));
    }

    [Fact]
    public async Task CambiarPinAsync_PinViejoIncorrecto_LanzaExcepcion()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CambiarPinAsync("9999", "5678"));
    }

    [Fact]
    public async Task DesactivarPinAsync_PinCorrecto_EliminaHash()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");

        await service.DesactivarPinAsync("1234");

        Assert.False(await service.EstaConfiguradoAsync());
    }

    [Fact]
    public async Task DesactivarPinAsync_PinIncorrecto_LanzaExcepcion()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DesactivarPinAsync("9999"));
    }

    [Fact]
    public void SesionEstaAutenticada_PorDefecto_DevuelveFalse()
    {
        var service = CrearService();
        Assert.False(service.SesionEstaAutenticada());
    }

    [Fact]
    public void EstablecerSesionAutenticada_MarcaSesion()
    {
        var service = CrearService();
        service.EstablecerSesionAutenticada();
        Assert.True(service.SesionEstaAutenticada());
    }

    [Fact]
    public void CerrarSesion_DesmarcaSesion()
    {
        var service = CrearService();
        service.EstablecerSesionAutenticada();
        service.CerrarSesion();
        Assert.False(service.SesionEstaAutenticada());
    }

    [Fact]
    public async Task GuardarDelayBloqueoSegundosAsync_ValorValido_Persiste()
    {
        var service = CrearService();
        await service.GuardarDelayBloqueoSegundosAsync(60);

        _usuarioServiceMock.Verify(s => s.GuardarUsuarioAsync(
            It.Is<Usuario>(u => u.PinDelaySegundos == 60)
        ), Times.Once);
    }

    [Fact]
    public async Task GuardarDelayBloqueoSegundosAsync_ValorInvalido_UsaDefault()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Nombre = "Test" };
        var mock = new Mock<IUsuarioService>();
        mock.Setup(s => s.ObtenerUsuarioAsync()).ReturnsAsync(usuario);
        mock.Setup(s => s.GuardarUsuarioAsync(It.IsAny<Usuario>())).Returns(Task.CompletedTask);

        var service = CrearServiceConStorage(_storage, mock);
        await service.GuardarDelayBloqueoSegundosAsync(999);

        Assert.Equal(30, usuario.PinDelaySegundos);
    }

    [Fact]
    public async Task VerificarPinAsync_SinConfigurar_DevuelveFalse()
    {
        var service = CrearService();
        Assert.False(await service.VerificarPinAsync("1234"));
    }

    [Fact]
    public async Task VerificarPinAsync_TiempoEspera_ConteoCorrecto()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");

        for (int i = 0; i < 5; i++)
        {
            await service.VerificarPinAsync("0000");
        }

        var espera = await service.ObtenerTiempoEsperaRestanteSegundosAsync();
        Assert.InRange(espera, 1, 31);
    }

    [Fact]
    public async Task VerificarPinAsync_TiempoEspera_SinBloqueo_DevuelveCero()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");

        var espera = await service.ObtenerTiempoEsperaRestanteSegundosAsync();
        Assert.Equal(0, espera);
    }

    [Fact]
    public async Task DesactivarPinAsync_LimpiaSesion()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");
        service.EstablecerSesionAutenticada();

        await service.DesactivarPinAsync("1234");

        Assert.False(service.SesionEstaAutenticada());
    }

    [Fact]
    public async Task BloqueadoYAcierto_ReiniciaTodo()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");

        for (int i = 0; i < 5; i++)
        {
            await service.VerificarPinAsync("0000");
        }
        Assert.True(await service.EstaTemporalmenteBloqueadoAsync());

        var espera = await service.ObtenerTiempoEsperaRestanteSegundosAsync();
        if (espera > 0)
        {
            await Task.Delay(espera * 1000 + 100);
        }

        Assert.True(await service.VerificarPinAsync("1234"));
        Assert.False(await service.EstaTemporalmenteBloqueadoAsync());
        Assert.Equal(0, await service.ObtenerIntentosFallidosAsync());
    }

    [Fact]
    public async Task ObtenerDelayBloqueoSegundosAsync_RetornaValorDelUsuario()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), PinDelaySegundos = 60 };
        var mock = new Mock<IUsuarioService>();
        mock.Setup(s => s.ObtenerUsuarioAsync()).ReturnsAsync(usuario);
        mock.Setup(s => s.GuardarUsuarioAsync(It.IsAny<Usuario>())).Returns(Task.CompletedTask);

        var service = CrearServiceConStorage(_storage, mock);
        var delay = await service.ObtenerDelayBloqueoSegundosAsync();

        Assert.Equal(60, delay);
    }

    [Fact]
    public async Task ObtenerDelayBloqueoSegundosAsync_UsuarioNulo_RetornaDefault()
    {
        _usuarioServiceMock.Setup(s => s.ObtenerUsuarioAsync()).ReturnsAsync((Usuario?)null);

        var service = CrearService();
        var delay = await service.ObtenerDelayBloqueoSegundosAsync();

        Assert.Equal(30, delay);
    }

    [Fact]
    public async Task GuardarDelayBloqueoSegundosAsync_CeroSegundos_Persiste()
    {
        var service = CrearService();
        await service.GuardarDelayBloqueoSegundosAsync(0);

        _usuarioServiceMock.Verify(s => s.GuardarUsuarioAsync(
            It.Is<Usuario>(u => u.PinDelaySegundos == 0)
        ), Times.Once);
    }

    [Fact]
    public async Task MultipleFallosNoExcedenMaxIntentos_NoBloquea()
    {
        var service = CrearService();
        await service.ConfigurarPinAsync("1234");

        for (int i = 0; i < 4; i++)
        {
            await service.VerificarPinAsync("0000");
        }

        Assert.False(await service.EstaTemporalmenteBloqueadoAsync());
    }

    [Fact]
    public async Task ConfigurarPinAsync_DosVeces_SobrescribeHash()
    {
        var service = CrearService();

        await service.ConfigurarPinAsync("1234");
        Assert.True(await service.VerificarPinAsync("1234"));

        await service.ConfigurarPinAsync("5678");
        Assert.True(await service.VerificarPinAsync("5678"));
        Assert.False(await service.VerificarPinAsync("1234"));
    }
}
