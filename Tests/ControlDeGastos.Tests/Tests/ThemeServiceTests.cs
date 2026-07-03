namespace ControlDeGastos.Tests.Tests;

public class ThemeServiceTests
{
    private readonly Mock<IUsuarioService> _usuarioMock = new();

    private ThemeService CrearService() => new(_usuarioMock.Object);

    [Fact]
    public async Task ObtenerClaseBodyAsync_ModoNormal_RetornaVacio()
    {
        _usuarioMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { ModoGamificadoActivo = false });

        var service = CrearService();
        var clase = await service.ObtenerClaseBodyAsync();

        Assert.Equal("", clase);
    }

    [Fact]
    public async Task ObtenerClaseBodyAsync_ModoRpg_RetornaRpgTheme()
    {
        _usuarioMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { ModoGamificadoActivo = true });

        var service = CrearService();
        var clase = await service.ObtenerClaseBodyAsync();

        Assert.Equal("rpg-theme", clase);
    }
}
