using Microsoft.Extensions.Logging;

namespace ControlDeGastos.Tests.Tests;

public class CategoriaServiceTests
{
    private readonly InMemoryStorageService _storage = new();
    private readonly Mock<IUsuarioService> _usuarioServiceMock = new();
    private readonly Mock<ISupabaseService> _supabaseMock = new();
    private readonly Mock<IServiceProvider> _serviceProviderMock = new();
    private readonly Mock<ISyncService> _syncMock = new();
    private readonly Guid _usuarioId = Guid.NewGuid();

    public CategoriaServiceTests()
    {
        _serviceProviderMock
            .Setup(s => s.GetService(typeof(ISyncService)))
            .Returns(_syncMock.Object);

        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Local });
    }

    private CategoriaService CrearService()
        => new(_storage, _usuarioServiceMock.Object, _supabaseMock.Object, _serviceProviderMock.Object, new Mock<ILogger<CategoriaService>>().Object);

    [Fact]
    public async Task InicializarCategoriasPorDefectoAsync_PrimeraVez_CreaDefault()
    {
        var service = CrearService();
        Assert.False(await _storage.KeyExistsAsync("cdg_categorias"));

        await service.InicializarCategoriasPorDefectoAsync();

        Assert.True(await _storage.KeyExistsAsync("cdg_categorias"));

        var categorias = await _storage.GetAsync<List<Categoria>>("cdg_categorias");
        Assert.NotNull(categorias);
        Assert.Equal(12, categorias!.Count);
        Assert.Contains(categorias, c => c.Nombre == "Comida");
        Assert.Contains(categorias, c => c.Nombre == "Salario");
    }

    [Fact]
    public async Task InicializarCategoriasPorDefectoAsync_YaExiste_NoSobrescribe()
    {
        var service = CrearService();
        var existentes = new List<Categoria>
        {
            new() { Nombre = "Mi categoría", Icono = "⭐", Color = "#fff", Tipo = TipoGasto.Gasto, Orden = 1 }
        };
        await _storage.SetAsync("cdg_categorias", existentes);

        await service.InicializarCategoriasPorDefectoAsync();

        var categorias = await _storage.GetAsync<List<Categoria>>("cdg_categorias");
        Assert.NotNull(categorias);
        Assert.Single(categorias!);
        Assert.Equal("Mi categoría", categorias![0].Nombre);
    }

    [Fact]
    public async Task CrearY_ObtenerCategoria_FlujoCompleto()
    {
        var service = CrearService();

        var categoria = new Categoria
        {
            Nombre = "Test",
            Icono = "🧪",
            Color = "#ff0000",
            Tipo = TipoGasto.Gasto,
            Orden = 99,
        };

        var creada = await service.CrearCategoriaAsync(categoria);
        Assert.NotNull(creada);
        Assert.Equal(_usuarioId, creada.UsuarioId);

        var lista = await service.ObtenerCategoriasAsync();
        Assert.Single(lista);
        Assert.Equal("Test", lista[0].Nombre);
        Assert.Equal("🧪", lista[0].Icono);
    }

    [Fact]
    public async Task EliminarCategoriaAsync_RemueveDeStorage()
    {
        var service = CrearService();

        var cat = await service.CrearCategoriaAsync(new Categoria
        {
            Nombre = "Eliminar",
            Icono = "❌",
            Color = "#000",
            Tipo = TipoGasto.Gasto,
        });

        Assert.Single(await service.ObtenerCategoriasAsync());

        await service.EliminarCategoriaAsync(cat.Id);
        Assert.Empty(await service.ObtenerCategoriasAsync());
    }

    [Fact]
    public async Task SubirOrden_IntercambiaConAnterior()
    {
        var service = CrearService();
        var a = await service.CrearCategoriaAsync(new Categoria { Nombre = "A", Icono = "1", Color = "#fff", Tipo = TipoGasto.Gasto, Orden = 1 });
        var b = await service.CrearCategoriaAsync(new Categoria { Nombre = "B", Icono = "2", Color = "#fff", Tipo = TipoGasto.Gasto, Orden = 2 });
        var c = await service.CrearCategoriaAsync(new Categoria { Nombre = "C", Icono = "3", Color = "#fff", Tipo = TipoGasto.Gasto, Orden = 3 });

        var hermanos = (await service.ObtenerCategoriasAsync()).Where(x => x.Tipo == TipoGasto.Gasto).OrderBy(x => x.Orden).ToList();
        var idx = hermanos.FindIndex(x => x.Id == b.Id);
        var arriba = hermanos[idx - 1];

        (arriba.Orden, b.Orden) = (b.Orden, arriba.Orden);
        await service.ActualizarCategoriaAsync(arriba);
        await service.ActualizarCategoriaAsync(b);

        var actual = await service.ObtenerCategoriasAsync();
        var ordenados = actual.Where(x => x.Tipo == TipoGasto.Gasto).OrderBy(x => x.Orden).ToList();
        Assert.Equal(b.Id, ordenados[0].Id);
        Assert.Equal(1, ordenados[0].Orden);
        Assert.Equal(arriba.Id, ordenados[1].Id);
        Assert.Equal(2, ordenados[1].Orden);
    }

    [Fact]
    public async Task BajarOrden_IntercambiaConSiguiente()
    {
        var service = CrearService();
        var a = await service.CrearCategoriaAsync(new Categoria { Nombre = "A", Icono = "1", Color = "#fff", Tipo = TipoGasto.Gasto, Orden = 1 });
        var b = await service.CrearCategoriaAsync(new Categoria { Nombre = "B", Icono = "2", Color = "#fff", Tipo = TipoGasto.Gasto, Orden = 2 });
        var c = await service.CrearCategoriaAsync(new Categoria { Nombre = "C", Icono = "3", Color = "#fff", Tipo = TipoGasto.Gasto, Orden = 3 });

        var hermanos = (await service.ObtenerCategoriasAsync()).Where(x => x.Tipo == TipoGasto.Gasto).OrderBy(x => x.Orden).ToList();
        var idx = hermanos.FindIndex(x => x.Id == b.Id);
        var abajo = hermanos[idx + 1];

        (abajo.Orden, b.Orden) = (b.Orden, abajo.Orden);
        await service.ActualizarCategoriaAsync(abajo);
        await service.ActualizarCategoriaAsync(b);

        var actual = await service.ObtenerCategoriasAsync();
        var ordenados = actual.Where(x => x.Tipo == TipoGasto.Gasto).OrderBy(x => x.Orden).ToList();
        Assert.Equal(a.Id, ordenados[0].Id);
        Assert.Equal(1, ordenados[0].Orden);
        Assert.Equal(abajo.Id, ordenados[1].Id);
        Assert.Equal(2, ordenados[1].Orden);
        Assert.Equal(b.Id, ordenados[2].Id);
        Assert.Equal(3, ordenados[2].Orden);
    }

    [Fact]
    public async Task SubirBajar_MismaCategoria_RestauraOrden()
    {
        var service = CrearService();
        var a = await service.CrearCategoriaAsync(new Categoria { Nombre = "A", Icono = "1", Color = "#fff", Tipo = TipoGasto.Gasto, Orden = 1 });
        var b = await service.CrearCategoriaAsync(new Categoria { Nombre = "B", Icono = "2", Color = "#fff", Tipo = TipoGasto.Gasto, Orden = 2 });

        var hermanos = (await service.ObtenerCategoriasAsync()).Where(x => x.Tipo == TipoGasto.Gasto).OrderBy(x => x.Orden).ToList();

        // subir B (swap con A)
        var idx = hermanos.FindIndex(x => x.Id == b.Id);
        var arriba = hermanos[idx - 1];
        (arriba.Orden, b.Orden) = (b.Orden, arriba.Orden);
        await service.ActualizarCategoriaAsync(arriba);
        await service.ActualizarCategoriaAsync(b);

        // bajar B (swap con A de nuevo)
        var actual = await service.ObtenerCategoriasAsync();
        var h2 = actual.Where(x => x.Tipo == TipoGasto.Gasto).OrderBy(x => x.Orden).ToList();
        var idx2 = h2.FindIndex(x => x.Id == b.Id);
        var abajo = h2[idx2 + 1];
        (abajo.Orden, b.Orden) = (b.Orden, abajo.Orden);
        await service.ActualizarCategoriaAsync(abajo);
        await service.ActualizarCategoriaAsync(b);

        var final = await service.ObtenerCategoriasAsync();
        var ordenFinal = final.Where(x => x.Tipo == TipoGasto.Gasto).OrderBy(x => x.Orden).ToList();
        Assert.Equal(a.Id, ordenFinal[0].Id);
        Assert.Equal(b.Id, ordenFinal[1].Id);
    }

    [Fact]
    public async Task TiposSeparados_SubirBajar_NoMezclaIngresosYGastos()
    {
        var service = CrearService();
        var comida = await service.CrearCategoriaAsync(new Categoria { Nombre = "Comida", Icono = "🍕", Color = "#fff", Tipo = TipoGasto.Gasto, Orden = 1 });
        var salario = await service.CrearCategoriaAsync(new Categoria { Nombre = "Salario", Icono = "💰", Color = "#fff", Tipo = TipoGasto.Ingreso, Orden = 1 });

        var todos = await service.ObtenerCategoriasAsync();
        var gastos = todos.Where(c => c.Tipo == TipoGasto.Gasto).OrderBy(c => c.Orden).ToList();
        var ingresos = todos.Where(c => c.Tipo == TipoGasto.Ingreso).OrderBy(c => c.Orden).ToList();

        Assert.Single(gastos);
        Assert.Single(ingresos);
        Assert.Equal("Comida", gastos[0].Nombre);
        Assert.Equal("Salario", ingresos[0].Nombre);
    }

    [Fact]
    public async Task ObtenerCategoriasAsync_ConHogar_FiltraPorHogar()
    {
        var hogarId = "hogar-test";
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, HogarId = hogarId });

        await _storage.SetAsync("cdg_categorias", new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nombre = "Compartida", Icono = "🍕", Color = "#fff", Tipo = TipoGasto.Gasto, HogarId = hogarId },
            new() { Id = Guid.NewGuid(), Nombre = "Global", Icono = "📦", Color = "#fff", Tipo = TipoGasto.Gasto, HogarId = null },
            new() { Id = Guid.NewGuid(), Nombre = "Otro hogar", Icono = "❌", Color = "#fff", Tipo = TipoGasto.Gasto, HogarId = "otro-hogar" },
        });

        var service = CrearService();
        var categorias = await service.ObtenerCategoriasAsync();

        Assert.Equal(2, categorias.Count);
        Assert.Contains(categorias, c => c.Nombre == "Compartida");
        Assert.Contains(categorias, c => c.Nombre == "Global");
    }

    [Fact]
    public async Task CrearCategoriaAsync_PlanNube_Sincroniza()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _supabaseMock
            .Setup(s => s.GuardarAsync("categorias", It.IsAny<Categoria>()))
            .ReturnsAsync((string _, Categoria c) => c);

        var service = CrearService();
        var cat = new Categoria { Nombre = "Test Sync", Icono = "🔄", Color = "#fff", Tipo = TipoGasto.Gasto };

        await service.CrearCategoriaAsync(cat);

        _supabaseMock.Verify(s => s.GuardarAsync("categorias", It.IsAny<Categoria>()), Times.Once);
    }

    [Fact]
    public async Task EliminarCategoriaAsync_PlanNube_EliminaEnSupabase()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        var service = CrearService();
        var cat = await service.CrearCategoriaAsync(new Categoria { Nombre = "A eliminar", Icono = "❌", Color = "#000", Tipo = TipoGasto.Gasto });

        await service.EliminarCategoriaAsync(cat.Id);

        _supabaseMock.Verify(s => s.EliminarAsync<Categoria>("categorias", cat.Id), Times.Once);
    }

    [Fact]
    public async Task ActualizarCategoriaAsync_ActualizaEnStorage()
    {
        var service = CrearService();
        var cat = await service.CrearCategoriaAsync(new Categoria { Nombre = "Original", Icono = "📁", Color = "#fff", Tipo = TipoGasto.Gasto });

        cat.Nombre = "Actualizada";
        cat.Icono = "✅";
        await service.ActualizarCategoriaAsync(cat);

        var lista = await service.ObtenerCategoriasAsync();
        Assert.Single(lista);
        Assert.Equal("Actualizada", lista[0].Nombre);
        Assert.Equal("✅", lista[0].Icono);
    }

    [Fact]
    public async Task ActualizarCategoriaAsync_IdNoExistente_NoFalla()
    {
        var service = CrearService();

        await service.ActualizarCategoriaAsync(new Categoria { Id = Guid.NewGuid(), Nombre = "Ghost", Icono = "👻", Color = "#fff", Tipo = TipoGasto.Gasto });

        Assert.Empty(await service.ObtenerCategoriasAsync());
    }

    [Fact]
    public async Task CrearCategoriaAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _supabaseMock
            .Setup(s => s.GuardarAsync("categorias", It.IsAny<Categoria>()))
            .ThrowsAsync(new Exception("Error de red"));

        var service = CrearService();
        var cat = new Categoria { Nombre = "Test", Icono = "🧪", Color = "#fff", Tipo = TipoGasto.Gasto };

        var ex = await Record.ExceptionAsync(() => service.CrearCategoriaAsync(cat));
        Assert.Null(ex);
        Assert.Single(await service.ObtenerCategoriasAsync());
    }

    [Fact]
    public async Task ActualizarCategoriaAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _supabaseMock
            .Setup(s => s.ActualizarAsync("categorias", It.IsAny<Guid>(), It.IsAny<Categoria>()))
            .ThrowsAsync(new Exception("Error de red"));

        var service = CrearService();
        var cat = await service.CrearCategoriaAsync(new Categoria { Nombre = "Original", Icono = "📁", Color = "#fff", Tipo = TipoGasto.Gasto });

        cat.Nombre = "Actualizada";
        var ex = await Record.ExceptionAsync(() => service.ActualizarCategoriaAsync(cat));
        Assert.Null(ex);
        Assert.Equal("Actualizada", (await service.ObtenerCategoriasAsync())[0].Nombre);
    }

    [Fact]
    public async Task EliminarCategoriaAsync_PlanNube_SupabaseFalla_NoLanzaExcepcion()
    {
        _usuarioServiceMock
            .Setup(s => s.ObtenerUsuarioAsync())
            .ReturnsAsync(new Usuario { Id = _usuarioId, PlanActivo = PlanType.Nube });

        _supabaseMock
            .Setup(s => s.EliminarAsync<Categoria>("categorias", It.IsAny<Guid>()))
            .ThrowsAsync(new Exception("Error de red"));

        var service = CrearService();
        var cat = await service.CrearCategoriaAsync(new Categoria { Nombre = "A eliminar", Icono = "❌", Color = "#000", Tipo = TipoGasto.Gasto });

        var ex = await Record.ExceptionAsync(() => service.EliminarCategoriaAsync(cat.Id));
        Assert.Null(ex);
        Assert.Empty(await service.ObtenerCategoriasAsync());
    }
}
