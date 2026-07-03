using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ControlDeGastos.Tests.Tests;

public class ExportImportServiceTests
{
    private readonly InMemoryStorageService _storage = new();

    public ExportImportServiceTests()
    {
        _storage.ClearAsync().GetAwaiter().GetResult();
    }

    private static DataMigrationRunner CrearRunner(IStorageService storage, bool incluirTodas = false)
    {
        if (incluirTodas)
        {
            return new(storage, new IDataMigration[]
            {
                new V1SeedMigration(),
                new V2NumeroVersionGastosMigration(),
                new V3NumeroVersionEntidadesMigration(),
                new V4SchemaVersionMigration(),
                new V5LimpiarReferenciasHuerfanasMigration(),
                new V7NormalizarDatosMigration(),
                new V8NombresMiembrosHogarMigration(),
            });
        }
        return new(storage, new IDataMigration[] { new V1SeedMigration(), new V2NumeroVersionGastosMigration() });
    }

    private ExportImportService CrearService() => new(_storage, CrearRunner(_storage));

    [Fact]
    public async Task ExportarDatosAsync_CuandoNoHayDatos_DevuelveJsonValido()
    {
        var service = CrearService();

        var bytes = await service.ExportarDatosAsync();

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        var json = Encoding.UTF8.GetString(bytes);
        var datos = JsonSerializer.Deserialize<DatosExportacion>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(datos);
        Assert.Equal(DataMigrationRunner.VersionActual, datos!.Version);
        Assert.Equal(DataMigrationRunner.VersionActual, datos.SchemaVersion);
        Assert.NotNull(datos.Datos);
        Assert.Empty(datos.Datos.Gastos);
        Assert.Empty(datos.Datos.Categorias);
        Assert.Empty(datos.Datos.Presupuestos);
        Assert.Empty(datos.Datos.Recurrencias);
        Assert.Empty(datos.Datos.Financiamientos);
        Assert.Empty(datos.Datos.BancosPersonalizados);
    }

    [Fact]
    public async Task ExportarDatosAsync_ConDatos_DevuelveTodoElContenido()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Nombre = "Test" };
        var categoria = new Categoria { Id = Guid.NewGuid(), Nombre = "Comida" };
        var gasto = new Gasto { Id = Guid.NewGuid(), Monto = 100, CategoriaId = categoria.Id };
        var presupuesto = new Presupuesto { Id = Guid.NewGuid(), MontoLimite = 1000 };
        var recurrencia = new Recurrencia { Id = Guid.NewGuid(), Monto = 200 };
        var financiamiento = new Financiamiento { Id = Guid.NewGuid(), MontoTotal = 5000 };
        var progreso = new ProgresoRPG { Id = Guid.NewGuid(), Nivel = 5 };
        var bancos = new List<string> { "Banco Test" };
        var tokens = new List<string> { "token123" };

        await _storage.SetAsync("cdg_usuario", usuario);
        await _storage.SetAsync("cdg_categorias", new List<Categoria> { categoria });
        await _storage.SetAsync("cdg_gastos", new List<Gasto> { gasto });
        await _storage.SetAsync("cdg_presupuestos", new List<Presupuesto> { presupuesto });
        await _storage.SetAsync("cdg_recurrencias", new List<Recurrencia> { recurrencia });
        await _storage.SetAsync("cdg_financiamientos", new List<Financiamiento> { financiamiento });
        await _storage.SetAsync("cdg_progreso_rpg", progreso);
        await _storage.SetAsync("cdg_bancos_personalizados", bancos);
        await _storage.SetAsync("cdg_used_tokens", tokens);

        var service = CrearService();

        var bytes = await service.ExportarDatosAsync();
        var json = Encoding.UTF8.GetString(bytes);
        var datos = JsonSerializer.Deserialize<DatosExportacion>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(datos);
        Assert.NotNull(datos!.Datos.Usuario);
        Assert.Equal("Test", datos.Datos.Usuario!.Nombre);
        Assert.Single(datos.Datos.Categorias);
        Assert.Single(datos.Datos.Gastos);
        Assert.Single(datos.Datos.Presupuestos);
        Assert.Single(datos.Datos.Recurrencias);
        Assert.Single(datos.Datos.Financiamientos);
        Assert.NotNull(datos.Datos.ProgresoRpg);
        Assert.Equal(5, datos.Datos.ProgresoRpg!.Nivel);
        Assert.Contains("Banco Test", datos.Datos.BancosPersonalizados);
        Assert.Contains("token123", datos.Datos.UsedTokens);
    }

    [Fact]
    public async Task ImportarDatosAsync_JsonValido_ReemplazaDatos()
    {
        await _storage.SetAsync("cdg_usuario", new Usuario { Id = Guid.NewGuid(), Nombre = "Viejo" });
        await _storage.SetAsync("cdg_gastos", new List<Gasto> { new() { Id = Guid.NewGuid(), Monto = 999 } });

        var datosNuevos = new DatosExportacion
        {
            Version = 1,
            ExportadoEn = DateTime.UtcNow,
            Datos = new DatosExportacionData
            {
                Usuario = new Usuario { Id = Guid.NewGuid(), Nombre = "Nuevo" },
                Gastos = new List<Gasto> { new() { Id = Guid.NewGuid(), Monto = 100 } },
                Categorias = new List<Categoria> { new() { Id = Guid.NewGuid(), Nombre = "Nueva Cat" } }
            }
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(datosNuevos);

        var service = CrearService();
        var resultado = await service.ImportarDatosAsync(bytes);

        Assert.True(resultado.Exito);
        Assert.Equal(1, resultado.TotalGastos);
        Assert.Equal(1, resultado.TotalCategorias);

        var usuario = await _storage.GetAsync<Usuario>("cdg_usuario");
        Assert.Equal("Nuevo", usuario!.Nombre);

        var gastos = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.Single(gastos!);
        Assert.Equal(100, gastos![0].Monto);

        var categorias = await _storage.GetAsync<List<Categoria>>("cdg_categorias");
        Assert.Single(categorias!);
        Assert.Equal("Nueva Cat", categorias![0].Nombre);
    }

    [Fact]
    public async Task ImportarDatosAsync_ArchivoVacio_DevuelveError()
    {
        var service = CrearService();

        var resultado = await service.ImportarDatosAsync(Array.Empty<byte>());

        Assert.False(resultado.Exito);
        Assert.Contains("formato JSON", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportarDatosAsync_Nulo_DevuelveError()
    {
        var service = CrearService();
        var bytes = Encoding.UTF8.GetBytes("null");

        var resultado = await service.ImportarDatosAsync(bytes);

        Assert.False(resultado.Exito);
        Assert.Contains("vacío", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportarDatosAsync_JsonInvalido_DevuelveError()
    {
        var service = CrearService();
        var bytes = Encoding.UTF8.GetBytes("{ esto no es json valido }");

        var resultado = await service.ImportarDatosAsync(bytes);

        Assert.False(resultado.Exito);
        Assert.Contains("JSON", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportarDatosAsync_VersionInvalida_DevuelveError()
    {
        var datos = new DatosExportacion
        {
            Version = 99,
            Datos = new DatosExportacionData()
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(datos);

        var service = CrearService();
        var resultado = await service.ImportarDatosAsync(bytes);

        Assert.False(resultado.Exito);
        Assert.Contains("versión", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportarDatosAsync_DatosNulos_DevuelveError()
    {
        var service = CrearService();
        var bytes = Encoding.UTF8.GetBytes("{\"version\":1,\"datos\":null}");

        var resultado = await service.ImportarDatosAsync(bytes);

        Assert.False(resultado.Exito);
        Assert.Contains("datos", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportarDatosAsync_ConTodosLosDatos_ImportaCorrectamente()
    {
        var datos = new DatosExportacion
        {
            Version = 1,
            Datos = new DatosExportacionData
            {
                Usuario = new Usuario { Id = Guid.NewGuid(), Nombre = "Test" },
                Categorias = new List<Categoria> { new() { Id = Guid.NewGuid(), Nombre = "Cat1" } },
                Gastos = new List<Gasto> { new() { Id = Guid.NewGuid(), Monto = 50 } },
                Presupuestos = new List<Presupuesto> { new() { Id = Guid.NewGuid(), MontoLimite = 500 } },
                Recurrencias = new List<Recurrencia> { new() { Id = Guid.NewGuid(), Monto = 100 } },
                Financiamientos = new List<Financiamiento> { new() { Id = Guid.NewGuid(), MontoTotal = 10000 } },
                ProgresoRpg = new ProgresoRPG { Id = Guid.NewGuid(), Nivel = 10 },
                BancosPersonalizados = new List<string> { "BancoX" },
                UsedTokens = new List<string> { "abc" },
                NotificacionesVistasMap = new Dictionary<string, List<Guid>>
                {
                    { "2026-01", new List<Guid> { Guid.NewGuid() } }
                }
            }
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(datos);

        var service = CrearService();
        var resultado = await service.ImportarDatosAsync(bytes);

        Assert.True(resultado.Exito);
        Assert.Equal(1, resultado.TotalGastos);
        Assert.Equal(1, resultado.TotalCategorias);
        Assert.Equal(1, resultado.TotalPresupuestos);
        Assert.Equal(1, resultado.TotalRecurrencias);
        Assert.Equal(1, resultado.TotalFinanciamientos);

        Assert.NotNull(await _storage.GetAsync<ProgresoRPG>("cdg_progreso_rpg"));
        var bancos = await _storage.GetAsync<List<string>>("cdg_bancos_personalizados");
        Assert.Contains("BancoX", bancos!);
        var tokens = await _storage.GetAsync<List<string>>("cdg_used_tokens");
        Assert.Contains("abc", tokens!);
        var notifMap = await _storage.GetAsync<Dictionary<string, List<Guid>>>("cdg_notif_vistas_map");
        Assert.NotNull(notifMap);
        Assert.Single(notifMap!);
    }

    [Fact]
    public async Task ImportarDatosAsync_BorraDatosViejos()
    {
        await _storage.SetAsync("cdg_gastos", new List<Gasto>
        {
            new() { Id = Guid.NewGuid(), Monto = 999 },
            new() { Id = Guid.NewGuid(), Monto = 500 }
        });
        await _storage.SetAsync("cdg_categorias", new List<Categoria>
        {
            new() { Id = Guid.NewGuid(), Nombre = "Vieja" }
        });
        await _storage.SetAsync("cdg_bancos_personalizados", new List<string> { "BancoViejo" });
        await _storage.SetAsync("cdg_usuario", new Usuario { Id = Guid.NewGuid(), Nombre = "Viejo" });

        var datosNuevos = new DatosExportacion
        {
            Version = 1,
            Datos = new DatosExportacionData
            {
                Usuario = new Usuario { Id = Guid.NewGuid(), Nombre = "Nuevo" },
                Gastos = new List<Gasto>
                {
                    new() { Id = Guid.NewGuid(), Monto = 100 }
                },
                Categorias = new List<Categoria>(),
                Presupuestos = new List<Presupuesto>(),
                Recurrencias = new List<Recurrencia>(),
                Financiamientos = new List<Financiamiento>()
            }
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(datosNuevos);
        var service = CrearService();
        var resultado = await service.ImportarDatosAsync(bytes);

        Assert.True(resultado.Exito);
        Assert.Equal(1, resultado.TotalGastos);
        Assert.Equal(0, resultado.TotalCategorias);

        var gastos = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.Single(gastos!);
        Assert.Equal(100, gastos![0].Monto);

        var categorias = await _storage.GetAsync<List<Categoria>>("cdg_categorias");
        Assert.NotNull(categorias);
        Assert.Empty(categorias!);

        var bancos = await _storage.GetAsync<List<string>>("cdg_bancos_personalizados");
        Assert.Null(bancos);

        var usuario = await _storage.GetAsync<Usuario>("cdg_usuario");
        Assert.Equal("Nuevo", usuario!.Nombre);
    }

    [Fact]
    public async Task ImportarDatosAsync_SinOpcionales_NoEscribeKeysAusentes()
    {
        var datos = new DatosExportacion
        {
            Version = 1,
            Datos = new DatosExportacionData
            {
                Gastos = new List<Gasto> { new() { Id = Guid.NewGuid(), Monto = 50 } },
                Categorias = new List<Categoria> { new() { Id = Guid.NewGuid(), Nombre = "Cat" } },
                Presupuestos = new List<Presupuesto>(),
                Recurrencias = new List<Recurrencia>(),
                Financiamientos = new List<Financiamiento>()
            }
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(datos);
        var service = CrearService();
        var resultado = await service.ImportarDatosAsync(bytes);

        Assert.True(resultado.Exito);

        var usuario = await _storage.GetAsync<Usuario>("cdg_usuario");
        Assert.Null(usuario);

        var progreso = await _storage.GetAsync<ProgresoRPG>("cdg_progreso_rpg");
        Assert.Null(progreso);

        var bancos = await _storage.GetAsync<List<string>>("cdg_bancos_personalizados");
        Assert.Null(bancos);

        var gastos = await _storage.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.Single(gastos!);
    }

    [Fact]
    public async Task ImportarDatosAsync_VersionCero_DevuelveError()
    {
        var datos = new DatosExportacion
        {
            Version = 0,
            Datos = new DatosExportacionData()
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(datos);
        var service = CrearService();
        var resultado = await service.ImportarDatosAsync(bytes);

        Assert.False(resultado.Exito);
        Assert.Contains("versión", resultado.Mensaje, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExportarEImportar_Roundtrip_ConservaDatos()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Nombre = "Roundtrip" };
        var categoria = new Categoria { Id = Guid.NewGuid(), Nombre = "Comida" };
        var gasto = new Gasto { Id = Guid.NewGuid(), Monto = 250, CategoriaId = categoria.Id };
        var presupuesto = new Presupuesto { Id = Guid.NewGuid(), MontoLimite = 2000 };
        var recurrencia = new Recurrencia { Id = Guid.NewGuid(), Monto = 300 };
        var financiamiento = new Financiamiento { Id = Guid.NewGuid(), MontoTotal = 15000 };

        await _storage.SetAsync("cdg_usuario", usuario);
        await _storage.SetAsync("cdg_categorias", new List<Categoria> { categoria });
        await _storage.SetAsync("cdg_gastos", new List<Gasto> { gasto });
        await _storage.SetAsync("cdg_presupuestos", new List<Presupuesto> { presupuesto });
        await _storage.SetAsync("cdg_recurrencias", new List<Recurrencia> { recurrencia });
        await _storage.SetAsync("cdg_financiamientos", new List<Financiamiento> { financiamiento });

        var exportBytes = await CrearService().ExportarDatosAsync();

        var storage2 = new InMemoryStorageService();
        var importService = new ExportImportService(storage2, CrearRunner(storage2));
        var resultado = await importService.ImportarDatosAsync(exportBytes);

        Assert.True(resultado.Exito);
        Assert.Equal(1, resultado.TotalGastos);
        Assert.Equal(1, resultado.TotalCategorias);
        Assert.Equal(1, resultado.TotalPresupuestos);
        Assert.Equal(1, resultado.TotalRecurrencias);
        Assert.Equal(1, resultado.TotalFinanciamientos);

        var usuarioImportado = await storage2.GetAsync<Usuario>("cdg_usuario");
        Assert.NotNull(usuarioImportado);
        Assert.Equal(usuario.Nombre, usuarioImportado!.Nombre);
        Assert.Equal(usuario.Id, usuarioImportado.Id);

        var gastosImportados = await storage2.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.Single(gastosImportados!);
        Assert.Equal(gasto.Monto, gastosImportados![0].Monto);
        Assert.Equal(gasto.Id, gastosImportados[0].Id);
    }

    [Fact]
    public async Task ExportarDatosAsync_VersionEsCorrecta()
    {
        var bytes = await CrearService().ExportarDatosAsync();
        var json = Encoding.UTF8.GetString(bytes);
        var datos = JsonSerializer.Deserialize<DatosExportacion>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(datos);
        Assert.Equal(DataMigrationRunner.VersionActual, datos!.Version);
        Assert.Equal(DataMigrationRunner.VersionActual, datos.SchemaVersion);
        Assert.True(datos.ExportadoEn > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task ExportarDatosAsync_ConProgresoRPG_IncluyeDatos()
    {
        var progreso = new ProgresoRPG
        {
            Id = Guid.NewGuid(),
            Nivel = 15,
            ExpActual = 750,
            HpActual = 80,
            LogrosDesbloqueados = new List<Guid> { Guid.NewGuid() }
        };
        await _storage.SetAsync("cdg_progreso_rpg", progreso);

        var bytes = await CrearService().ExportarDatosAsync();
        var json = Encoding.UTF8.GetString(bytes);
        var datos = JsonSerializer.Deserialize<DatosExportacion>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(datos!.Datos.ProgresoRpg);
        Assert.Equal(15, datos.Datos.ProgresoRpg!.Nivel);
        Assert.Equal(750, datos.Datos.ProgresoRpg.ExpActual);
        Assert.Single(datos.Datos.ProgresoRpg.LogrosDesbloqueados);
    }

    [Fact]
    public async Task ExportarDatosAsync_NoIncluyePinHash()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Nombre = "Test" };
        await _storage.SetAsync("cdg_usuario", usuario);

        var usuarioServiceMock = new Mock<IUsuarioService>();
        usuarioServiceMock.Setup(s => s.ObtenerUsuarioAsync()).ReturnsAsync(usuario);
        usuarioServiceMock.Setup(s => s.GuardarUsuarioAsync(It.IsAny<Usuario>())).Returns(Task.CompletedTask);

        var pinLockService = new PinLockService(_storage, usuarioServiceMock.Object);
        await pinLockService.ConfigurarPinAsync("1234");

        var bytes = await CrearService().ExportarDatosAsync();
        var json = Encoding.UTF8.GetString(bytes);

        Assert.DoesNotContain("cdg_pin_hash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("pinHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1234", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportarDatosAsync_PinHashConfigurado_NoSePierde()
    {
        var usuario = new Usuario { Id = Guid.NewGuid(), Nombre = "Original" };
        await _storage.SetAsync("cdg_usuario", usuario);

        var usuarioServiceMock = new Mock<IUsuarioService>();
        usuarioServiceMock.Setup(s => s.ObtenerUsuarioAsync()).ReturnsAsync(usuario);
        usuarioServiceMock.Setup(s => s.GuardarUsuarioAsync(It.IsAny<Usuario>())).Returns(Task.CompletedTask);

        var pinLockService = new PinLockService(_storage, usuarioServiceMock.Object);
        await pinLockService.ConfigurarPinAsync("1234");

        var exportBytes = await CrearService().ExportarDatosAsync();
        var storage2 = new InMemoryStorageService();
        var importService = new ExportImportService(storage2, CrearRunner(storage2));
        var resultado = await importService.ImportarDatosAsync(exportBytes);

        Assert.True(resultado.Exito);

        var pinService2 = new PinLockService(storage2, Mock.Of<IUsuarioService>());
        Assert.False(await pinService2.EstaConfiguradoAsync());
    }

    [Fact]
    public async Task ImportarDatosAsync_VersionInferior_EjecutaMigraciones()
    {
        var catId = Guid.NewGuid();
        var recId = Guid.NewGuid();
        var finId = Guid.NewGuid();

        var datos = new DatosExportacion
        {
            Version = 1,
            Datos = new DatosExportacionData
            {
                Gastos = new List<Gasto>
                {
                    new() { Id = Guid.NewGuid(), Monto = 100, CategoriaId = catId, NumeroVersion = 0, SchemaVersion = 0,
                            RecurrenciaId = recId, FinanciamientoId = finId }
                },
                Categorias = new List<Categoria>
                {
                    new() { Id = catId, Nombre = "Cat", NumeroVersion = 0, SchemaVersion = 0 }
                },
                Presupuestos = new List<Presupuesto>
                {
                    new() { Id = Guid.NewGuid(), MontoLimite = 1000, CategoriaId = catId, NumeroVersion = 0, SchemaVersion = 0 }
                },
                Recurrencias = new List<Recurrencia>
                {
                    new() { Id = recId, Monto = 200, NumeroVersion = 0, SchemaVersion = 0 }
                },
                Financiamientos = new List<Financiamiento>
                {
                    new() { Id = finId, MontoTotal = 5000, NumeroVersion = 0, SchemaVersion = 0 }
                },
            }
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(datos);
        var storageDestino = new InMemoryStorageService();
        var service = new ExportImportService(storageDestino, CrearRunner(storageDestino, incluirTodas: true));
        var resultado = await service.ImportarDatosAsync(bytes);

        Assert.True(resultado.Exito);

        var gastos = await storageDestino.GetAsync<List<Gasto>>("cdg_gastos");
        Assert.NotNull(gastos);
        Assert.Single(gastos!);
        Assert.Equal(1, gastos![0].NumeroVersion);
        Assert.Equal(DataMigrationRunner.VersionActual, gastos[0].SchemaVersion);

        var categorias = await storageDestino.GetAsync<List<Categoria>>("cdg_categorias");
        Assert.NotNull(categorias);
        Assert.Equal(1, categorias![0].NumeroVersion);
        Assert.Equal(DataMigrationRunner.VersionActual, categorias[0].SchemaVersion);

        var version = await storageDestino.GetAsync<int>("cdg_data_version");
        Assert.Equal(DataMigrationRunner.VersionActual, version);
    }

    [Fact]
    public async Task ImportarDatosAsync_VersionActual_NoEjecutaMigraciones()
    {
        var datos = new DatosExportacion
        {
            Version = DataMigrationRunner.VersionActual,
            Datos = new DatosExportacionData
            {
                Gastos = new List<Gasto> { new() { Id = Guid.NewGuid(), Monto = 100, NumeroVersion = 0 } },
                Categorias = new List<Categoria> { new() { Id = Guid.NewGuid(), Nombre = "Cat" } }
            }
        };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(datos);
        var storageDestino = new InMemoryStorageService();
        var service = new ExportImportService(storageDestino, CrearRunner(storageDestino, incluirTodas: true));
        var resultado = await service.ImportarDatosAsync(bytes);

        Assert.True(resultado.Exito);

        var version = await storageDestino.GetAsync<int>("cdg_data_version");
        Assert.Equal(0, version); // no se escribio porque no hubo migracion
    }

    [Fact]
    public async Task ExportarDatosAsync_EntidadesLlevanSchemaVersion()
    {
        var categoria = new Categoria { Id = Guid.NewGuid(), Nombre = "Comida" };
        var gasto = new Gasto { Id = Guid.NewGuid(), Monto = 100, CategoriaId = categoria.Id };
        var presupuesto = new Presupuesto { Id = Guid.NewGuid(), MontoLimite = 1000 };
        var recurrencia = new Recurrencia { Id = Guid.NewGuid(), Monto = 200 };
        var financiamiento = new Financiamiento { Id = Guid.NewGuid(), MontoTotal = 5000 };
        var progreso = new ProgresoRPG { Id = Guid.NewGuid(), Nivel = 5 };

        await _storage.SetAsync("cdg_categorias", new List<Categoria> { categoria });
        await _storage.SetAsync("cdg_gastos", new List<Gasto> { gasto });
        await _storage.SetAsync("cdg_presupuestos", new List<Presupuesto> { presupuesto });
        await _storage.SetAsync("cdg_recurrencias", new List<Recurrencia> { recurrencia });
        await _storage.SetAsync("cdg_financiamientos", new List<Financiamiento> { financiamiento });
        await _storage.SetAsync("cdg_progreso_rpg", progreso);

        var bytes = await CrearService().ExportarDatosAsync();
        var json = Encoding.UTF8.GetString(bytes);
        var datos = JsonSerializer.Deserialize<DatosExportacion>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(datos);
        Assert.Equal(DataMigrationRunner.VersionActual, datos!.Datos.Gastos[0].SchemaVersion);
        Assert.Equal(DataMigrationRunner.VersionActual, datos.Datos.Categorias[0].SchemaVersion);
        Assert.Equal(DataMigrationRunner.VersionActual, datos.Datos.Presupuestos[0].SchemaVersion);
        Assert.Equal(DataMigrationRunner.VersionActual, datos.Datos.Recurrencias[0].SchemaVersion);
        Assert.Equal(DataMigrationRunner.VersionActual, datos.Datos.Financiamientos[0].SchemaVersion);
        Assert.Equal(DataMigrationRunner.VersionActual, datos.Datos.ProgresoRpg!.SchemaVersion);
    }
}
