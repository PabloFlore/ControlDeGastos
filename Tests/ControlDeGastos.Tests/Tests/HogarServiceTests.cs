using Microsoft.Extensions.Logging;

namespace ControlDeGastos.Tests.Tests;

public class HogarServiceTests
{
    private readonly Mock<ISupabaseService> _supabaseMock = new();
    private readonly Mock<IUsuarioService> _usuarioMock = new();
    private readonly string _hogarId = Guid.NewGuid().ToString();
    private readonly string _email = "test@example.com";
    private readonly Guid _usuarioId = Guid.NewGuid();

    private HogarService CrearService() => new(_supabaseMock.Object, _usuarioMock.Object, new Mock<ILogger<HogarService>>().Object);

    private HogarService.HogarRow CrearHogar(string? creadoPor = null) => new()
    {
        Id = _hogarId,
        CodigoInvitacion = "ABC123",
        CreadoPorEmail = creadoPor ?? _email,
        CreatedAt = DateTime.UtcNow.ToString("O"),
    };

    [Fact]
    public async Task ObtenerHogarAsync_Existe_RetornaHogar()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.HogarRow> { CrearHogar() });

        var service = CrearService();
        var hogar = await service.ObtenerHogarAsync(_hogarId);

        Assert.NotNull(hogar);
        Assert.Equal(_hogarId, hogar!.Id);
        Assert.Equal("ABC123", hogar.CodigoInvitacion);
    }

    [Fact]
    public async Task ObtenerHogarAsync_NoExiste_RetornaNull()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.HogarRow>());

        var service = CrearService();
        var hogar = await service.ObtenerHogarAsync(_hogarId);

        Assert.Null(hogar);
    }

    [Fact]
    public async Task ObtenerHogarPorCodigoAsync_Existe_RetornaHogar()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares",
                It.Is<string>(f => f.Contains("codigo_invitacion")), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.HogarRow> { CrearHogar() });

        var service = CrearService();
        var hogar = await service.ObtenerHogarPorCodigoAsync("abc123");

        Assert.NotNull(hogar);
        Assert.Equal(_hogarId, hogar!.Id);
    }

    [Fact]
    public async Task ObtenerHogarPorCodigoAsync_NoExiste_RetornaNull()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.HogarRow>());

        var service = CrearService();
        var hogar = await service.ObtenerHogarPorCodigoAsync("NOEXISTE");

        Assert.Null(hogar);
    }

    [Fact]
    public async Task CrearHogarAsync_GuardaHogarYMiembro()
    {
        HogarService.HogarRow? hogarGuardado = null;
        HogarService.MiembroRow? miembroGuardado = null;
        _supabaseMock
            .Setup(s => s.GuardarAsync("hogares", It.IsAny<HogarService.HogarRow>()))
            .Callback<string, HogarService.HogarRow>((_, item) => hogarGuardado = item)
            .ReturnsAsync((string _, HogarService.HogarRow item) => item);
        _supabaseMock
            .Setup(s => s.GuardarAsync("hogar_miembros", It.IsAny<HogarService.MiembroRow>()))
            .Callback<string, HogarService.MiembroRow>((_, item) => miembroGuardado = item)
            .ReturnsAsync((string _, HogarService.MiembroRow item) => item);
        _supabaseMock
            .Setup(s => s.ObtenerEmailSesionAsync())
            .ReturnsAsync(_email);
        _usuarioMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, Email = _email, Nombre = "Test" });

        var service = CrearService();
        var hogar = await service.CrearHogarAsync();

        Assert.NotNull(hogar);
        Assert.Equal(_email, hogar.CreadoPorEmail);
        Assert.Equal(8, hogar.CodigoInvitacion.Length);
        Assert.NotNull(hogarGuardado);
        Assert.Equal(_email, hogarGuardado!.CreadoPorEmail);
        Assert.NotNull(miembroGuardado);
        Assert.Equal(_email, miembroGuardado!.Email);
    }

    [Fact]
    public async Task UnirseAHogarAsync_CodigoValidoNoEsMiembro_RetornaTrue()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.HogarRow> { CrearHogar("otro@example.com") });
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.MiembroRow>("hogar_miembros", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.MiembroRow>());

        var service = CrearService();
        var resultado = await service.UnirseAHogarAsync("ABC123", _email);

        Assert.True(resultado);
        _supabaseMock.Verify(s => s.GuardarAsync("hogar_miembros", It.IsAny<HogarService.MiembroRow>()), Times.Once);
    }

    [Fact]
    public async Task UnirseAHogarAsync_CodigoInvalido_RetornaFalse()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.HogarRow>());

        var service = CrearService();
        var resultado = await service.UnirseAHogarAsync("MALO", _email);

        Assert.False(resultado);
        _supabaseMock.Verify(s => s.GuardarAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task UnirseAHogarAsync_YaEsMiembro_RetornaFalse()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.HogarRow> { CrearHogar("otro@example.com") });
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.MiembroRow>("hogar_miembros", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.MiembroRow>
            {
                new() { HogarId = _hogarId, Email = _email, JoinedAt = DateTime.UtcNow.ToString("O") }
            });

        var service = CrearService();
        var resultado = await service.UnirseAHogarAsync("ABC123", _email);

        Assert.False(resultado);
        _supabaseMock.Verify(s => s.GuardarAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task SalirDelHogarAsync_HogarNoExiste_RetornaFalse()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.HogarRow>());

        var service = CrearService();
        var resultado = await service.SalirDelHogarAsync(_hogarId, _email);

        Assert.False(resultado);
    }

    [Fact]
    public async Task SalirDelHogarAsync_EsCreador_NoPermiteSalir()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.HogarRow> { CrearHogar() });

        var service = CrearService();
        var resultado = await service.SalirDelHogarAsync(_hogarId, _email);

        Assert.False(resultado);
    }

    [Fact]
    public async Task SalirDelHogarAsync_EsMiembroNoCreador_RetornaTrue()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.HogarRow> { CrearHogar("creador@example.com") });

        var service = CrearService();
        var resultado = await service.SalirDelHogarAsync(_hogarId, _email);

        Assert.True(resultado);
        _supabaseMock.Verify(
            s => s.EliminarConFiltroAsync<HogarService.MiembroRow>("hogar_miembros",
                It.Is<string>(f => f.Contains(_hogarId) && f.Contains(Uri.EscapeDataString(_email)))),
            Times.Once);
    }

    [Fact]
    public async Task GuardarLicenciaHogarAsync_LlamaActualizar()
    {
        var licencia = new Licencia
        {
            Token = "HOGAR-test",
            TokenHash = "hash123",
            LicenciaTipo = TipoLicencia.ParaSiempre,
            PlanIncluido = PlanType.Nube,
            ModoGamificadoIncluido = true
        };

        var service = CrearService();
        await service.GuardarLicenciaHogarAsync(_hogarId, licencia);

        _supabaseMock.Verify(
            s => s.ActualizarAsync("hogares", _hogarId, It.IsAny<HogarService.HogarRow>()),
            Times.Once);
    }

    [Fact]
    public async Task ObtenerLicenciaHogarAsync_Existe_RetornaLicencia()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.HogarRow>
            {
                new()
                {
                    Id = _hogarId,
                    CodigoInvitacion = "ABC123",
                    CreadoPorEmail = _email,
                    CreatedAt = DateTime.UtcNow.ToString("O"),
                    TokenHash = "hash123",
                    LicenciaTipo = "ParaSiempre",
                    PlanIncluido = "Nube",
                    ModoGamificadoIncluido = true,
                }
            });

        var service = CrearService();
        var licencia = await service.ObtenerLicenciaHogarAsync(_hogarId);

        Assert.NotNull(licencia);
        Assert.Equal("hash123", licencia!.TokenHash);
        Assert.Equal(TipoLicencia.ParaSiempre, licencia.LicenciaTipo);
        Assert.Equal(PlanType.Nube, licencia.PlanIncluido);
        Assert.True(licencia.ModoGamificadoIncluido);
    }

    [Fact]
    public async Task ObtenerLicenciaHogarAsync_SinDatos_RetornaNull()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.HogarRow>());

        var service = CrearService();
        var licencia = await service.ObtenerLicenciaHogarAsync(_hogarId);

        Assert.Null(licencia);
    }

    [Fact]
    public async Task ObtenerMiembrosAsync_RetornaListaMapeada()
    {
        var now = DateTime.UtcNow;
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.MiembroRow>("hogar_miembros", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.MiembroRow>
            {
                new() { HogarId = _hogarId, Email = "a@test.com", JoinedAt = now.ToString("O") },
                new() { HogarId = _hogarId, Email = "b@test.com", JoinedAt = now.AddDays(-1).ToString("O") },
            });

        var service = CrearService();
        var miembros = await service.ObtenerMiembrosAsync(_hogarId);

        Assert.Equal(2, miembros.Count);
        Assert.Equal("a@test.com", miembros[0].Email);
        Assert.Equal(_hogarId, miembros[0].HogarId);
        Assert.Equal(now.Date, miembros[0].JoinedAt.Date);
    }

    [Fact]
    public async Task SalirDelHogarAsync_SupabaseFalla_RetornaFalse()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new Exception("Error de red"));

        var service = CrearService();
        var resultado = await service.SalirDelHogarAsync(_hogarId, _email);

        Assert.False(resultado);
    }

    [Fact]
    public async Task GuardarLicenciaHogarAsync_SupabaseFalla_NoLanzaExcepcion()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.HogarRow> { CrearHogar() });

        _supabaseMock
            .Setup(s => s.ActualizarAsync("hogares", It.IsAny<string>(), It.IsAny<HogarService.HogarRow>()))
            .ThrowsAsync(new Exception("Error de red"));

        var service = CrearService();
        var licencia = new Licencia
        {
            Token = "HOGAR-test", TokenHash = "hash123",
            LicenciaTipo = TipoLicencia.ParaSiempre, PlanIncluido = PlanType.Nube,
        };

        var ex = await Record.ExceptionAsync(() => service.GuardarLicenciaHogarAsync(_hogarId, licencia));
        Assert.Null(ex);
    }

    [Fact]
    public async Task ObtenerLicenciaHogarAsync_SupabaseFalla_RetornaNull()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ThrowsAsync(new Exception("Error de red"));

        var service = CrearService();
        var licencia = await service.ObtenerLicenciaHogarAsync(_hogarId);

        Assert.Null(licencia);
    }

    [Fact]
    public async Task ObtenerLicenciaHogarAsync_EnumInvalido_UsaDefaults()
    {
        _supabaseMock
            .Setup(s => s.ObtenerTodosAsync<HogarService.HogarRow>("hogares", It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new List<HogarService.HogarRow>
            {
                new()
                {
                    Id = _hogarId,
                    CodigoInvitacion = "ABC123",
                    CreadoPorEmail = _email,
                    CreatedAt = DateTime.UtcNow.ToString("O"),
                    TokenHash = "hash123",
                    LicenciaTipo = "INVALIDO",
                    PlanIncluido = "INVALIDO",
                    ModoGamificadoIncluido = true,
                }
            });

        var service = CrearService();
        var licencia = await service.ObtenerLicenciaHogarAsync(_hogarId);

        Assert.NotNull(licencia);
        Assert.Equal(TipoLicencia.Trial, licencia!.LicenciaTipo);
        Assert.Equal(PlanType.Nube, licencia.PlanIncluido);
    }
}
