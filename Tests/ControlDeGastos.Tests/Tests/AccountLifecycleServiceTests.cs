using ControlDeGastos.Models;
using ControlDeGastos.Services;
using Moq;

namespace ControlDeGastos.Tests.Tests;

public class AccountLifecycleServiceTests
{
    private readonly Mock<IUsuarioService> _usuarioServiceMock = new();
    private readonly Mock<ISupabaseService> _supabaseServiceMock = new();
    private readonly Mock<IGastoService> _gastoServiceMock = new();
    private readonly Mock<IHogarService> _hogarServiceMock = new();
    private readonly Mock<ILicenciaService> _licenciaServiceMock = new();
    private readonly Mock<IStorageService> _storageServiceMock = new();
    private readonly Mock<ISyncService> _syncServiceMock = new();
    private readonly Mock<IPresupuestoService> _presupuestoServiceMock = new();
    private readonly Mock<ICategoriaService> _categoriaServiceMock = new();
    private readonly Mock<IRecurrenciaService> _recurrenciaServiceMock = new();
    private readonly Mock<IFinanciamientoService> _financiamientoServiceMock = new();
    private readonly Guid _usuarioId = Guid.NewGuid();

    public AccountLifecycleServiceTests()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Local });
    }

    private AccountLifecycleService CrearService()
        => new(
            _usuarioServiceMock.Object,
            _supabaseServiceMock.Object,
            _gastoServiceMock.Object,
            _hogarServiceMock.Object,
            _licenciaServiceMock.Object,
            _storageServiceMock.Object,
            _syncServiceMock.Object,
            _presupuestoServiceMock.Object,
            _categoriaServiceMock.Object,
            _recurrenciaServiceMock.Object,
            _financiamientoServiceMock.Object);

    [Fact]
    public async Task ConnectCloudAsync_Exitoso_GuardaUsuarioYMarcas()
    {
        _supabaseServiceMock
            .Setup(s => s.IniciarSesionAsync("a@b.com", "pass"))
            .ReturnsAsync(true);

        var service = CrearService();
        var result = await service.ConnectCloudAsync("a@b.com", "pass");

        Assert.True(result.Success);
        _usuarioServiceMock.Verify(s => s.GuardarUsuarioAsync(It.Is<Usuario>(u =>
            u.Email == "a@b.com" && u.PlanActivo == PlanType.Nube)), Times.Once);
        _gastoServiceMock.Verify(s => s.MarcarTodosPendientesSyncAsync(), Times.Once);
    }

    [Fact]
    public async Task ConnectCloudAsync_SupabaseFalla_RetornaError()
    {
        _supabaseServiceMock
            .Setup(s => s.IniciarSesionAsync("a@b.com", "bad"))
            .ReturnsAsync(false);

        var service = CrearService();
        var result = await service.ConnectCloudAsync("a@b.com", "bad");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        _usuarioServiceMock.Verify(s => s.GuardarUsuarioAsync(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task ConnectCloudAsync_UsuarioNulo_RetornaError()
    {
        _supabaseServiceMock
            .Setup(s => s.IniciarSesionAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync((Usuario?)null);

        var service = CrearService();
        var result = await service.ConnectCloudAsync("a@b.com", "pass");

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DisconnectCloudAsync_LimpiaUsuarioYDesconecta()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube, Email = "a@b.com", HogarId = "hogar1", HogarCodigo = "ABC123" });

        var service = CrearService();
        var result = await service.DisconnectCloudAsync();

        Assert.True(result.Success);
        _supabaseServiceMock.Verify(s => s.CerrarSesionAsync(), Times.Once);
        _usuarioServiceMock.Verify(s => s.GuardarUsuarioAsync(It.Is<Usuario>(u =>
            u.Email == null && u.HogarId == null && u.HogarCodigo == null && u.PlanActivo == PlanType.Local)), Times.Once);
    }

    [Fact]
    public async Task ToggleGamificationAsync_ActivarSinLicencia_RetornaError()
    {
        _licenciaServiceMock
            .Setup(s => s.ObtenerEstadoLicenciaAsync())
            .ReturnsAsync(new Models.Licencia { Valida = false });

        var service = CrearService();
        var result = await service.ToggleGamificationAsync(true);

        Assert.False(result.Success);
        _usuarioServiceMock.Verify(s => s.CambiarModoGamificadoAsync(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ToggleGamificationAsync_ActivarConLicenciaCorrecta_Exitoso()
    {
        _licenciaServiceMock
            .Setup(s => s.ObtenerEstadoLicenciaAsync())
            .ReturnsAsync(new Models.Licencia { Valida = true, ModoGamificadoIncluido = true });

        var service = CrearService();
        var result = await service.ToggleGamificationAsync(true);

        Assert.True(result.Success);
        _usuarioServiceMock.Verify(s => s.CambiarModoGamificadoAsync(true), Times.Once);
    }

    [Fact]
    public async Task ToggleGamificationAsync_Desactivar_SiempreExitoso()
    {
        var service = CrearService();
        var result = await service.ToggleGamificationAsync(false);

        Assert.True(result.Success);
        _licenciaServiceMock.Verify(s => s.ObtenerEstadoLicenciaAsync(), Times.Never);
        _usuarioServiceMock.Verify(s => s.CambiarModoGamificadoAsync(false), Times.Once);
    }

    [Fact]
    public async Task CreateHouseholdAsync_CreaYAsignaAlUsuario()
    {
        var hogarId = "hogar-test";
        var codigo = "ABC123";
        _hogarServiceMock
            .Setup(s => s.CrearHogarAsync())
            .ReturnsAsync(new Hogar { Id = hogarId, CodigoInvitacion = codigo });

        var service = CrearService();
        var result = await service.CreateHouseholdAsync();

        Assert.True(result.Success);
        _usuarioServiceMock.Verify(s => s.GuardarUsuarioAsync(It.Is<Usuario>(u =>
            u.HogarId == hogarId && u.HogarCodigo == codigo)), Times.Once);
        _gastoServiceMock.Verify(s => s.MigrarGastosAHogarAsync(hogarId), Times.Once);
        _presupuestoServiceMock.Verify(s => s.MigrarPresupuestosAHogarAsync(hogarId), Times.Once);
        _categoriaServiceMock.Verify(s => s.MigrarCategoriasAHogarAsync(hogarId), Times.Once);
        _recurrenciaServiceMock.Verify(s => s.MigrarRecurrenciasAHogarAsync(hogarId), Times.Once);
        _financiamientoServiceMock.Verify(s => s.MigrarFinanciamientosAHogarAsync(hogarId), Times.Once);
        _syncServiceMock.Verify(s => s.SincronizarAhoraAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task JoinHouseholdAsync_CodigoValido_ActualizaUsuario()
    {
        var hogarId = "hogar-test";
        var codigo = "ABC123";

        _hogarServiceMock
            .Setup(s => s.UnirseAHogarAsync(codigo, "a@b.com"))
            .ReturnsAsync(true);

        _hogarServiceMock
            .Setup(s => s.ObtenerHogarPorCodigoAsync(codigo))
            .ReturnsAsync(new Hogar { Id = hogarId, CodigoInvitacion = codigo });

        var service = CrearService();
        var result = await service.JoinHouseholdAsync(codigo, "a@b.com");

        Assert.True(result.Success);
        _usuarioServiceMock.Verify(s => s.GuardarUsuarioAsync(It.Is<Usuario>(u =>
            u.HogarId == hogarId && u.HogarCodigo == codigo)), Times.Once);
        _gastoServiceMock.Verify(s => s.MigrarGastosAHogarAsync(hogarId), Times.Once);
        _presupuestoServiceMock.Verify(s => s.MigrarPresupuestosAHogarAsync(hogarId), Times.Once);
        _categoriaServiceMock.Verify(s => s.MigrarCategoriasAHogarAsync(hogarId), Times.Once);
        _recurrenciaServiceMock.Verify(s => s.MigrarRecurrenciasAHogarAsync(hogarId), Times.Once);
        _financiamientoServiceMock.Verify(s => s.MigrarFinanciamientosAHogarAsync(hogarId), Times.Once);
        _syncServiceMock.Verify(s => s.SincronizarAhoraAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task JoinHouseholdAsync_CodigoInvalido_RetornaError()
    {
        _hogarServiceMock
            .Setup(s => s.UnirseAHogarAsync("BAD", "a@b.com"))
            .ReturnsAsync(false);

        var service = CrearService();
        var result = await service.JoinHouseholdAsync("BAD", "a@b.com");

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
        _usuarioServiceMock.Verify(s => s.GuardarUsuarioAsync(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task LeaveHouseholdAsync_Exitoso_LimpiaUsuario()
    {
        _hogarServiceMock
            .Setup(s => s.SalirDelHogarAsync("hogar1", "a@b.com"))
            .ReturnsAsync(true);

        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, HogarId = "hogar1", HogarCodigo = "ABC" });

        var service = CrearService();
        var result = await service.LeaveHouseholdAsync("hogar1", "a@b.com");

        Assert.True(result.Success);
        _usuarioServiceMock.Verify(s => s.GuardarUsuarioAsync(It.Is<Usuario>(u =>
            u.HogarId == null && u.HogarCodigo == null)), Times.Once);
    }

    [Fact]
    public async Task LeaveHouseholdAsync_CreadorNoPuede_RetornaError()
    {
        _hogarServiceMock
            .Setup(s => s.SalirDelHogarAsync("hogar1", "owner@b.com"))
            .ReturnsAsync(false);

        var service = CrearService();
        var result = await service.LeaveHouseholdAsync("hogar1", "owner@b.com");

        Assert.False(result.Success);
        _usuarioServiceMock.Verify(s => s.GuardarUsuarioAsync(It.IsAny<Usuario>()), Times.Never);
    }

    [Fact]
    public async Task LogoutAndClearAsync_LimpiaTodo()
    {
        _storageServiceMock
            .Setup(s => s.KeyExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        var service = CrearService();
        var result = await service.LogoutAndClearAsync();

        Assert.True(result.Success);
        _supabaseServiceMock.Verify(s => s.CerrarSesionAsync(), Times.Once);
        _storageServiceMock.Verify(s => s.RemoveAsync(It.IsAny<string>()), Times.AtLeastOnce);
    }
}
