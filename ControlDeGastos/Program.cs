using Blazored.Toast;
using ControlDeGastos;
using ControlDeGastos.Services;
using ControlDeGastos.Models;
using ControlDeGastos.Services.DataMigration;
using ControlDeGastos.Services.DataMigration.Migrations;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddBlazoredToast();

builder.Services.AddScoped<ILicenciaService>(sp =>
{
    var storage = sp.GetRequiredService<IStorageService>();
    var logger = sp.GetRequiredService<ILogger<LicenciaService>>();
    var publicKeyBytes = new byte[] { 48, 89, 48, 19, 6, 7, 42, 134, 72, 206, 61, 2, 1, 6, 8, 42, 134, 72, 206, 61, 3, 1, 7, 3, 66, 0, 4, 228, 32, 164, 132, 157, 64, 233, 225, 242, 89, 10, 191, 20, 113, 221, 241, 196, 55, 220, 43, 181, 219, 225, 111, 115, 119, 210, 100, 129, 129, 7, 101, 0, 100, 225, 3, 183, 2, 225, 117, 130, 31, 13, 6, 192, 194, 76, 237, 29, 136, 206, 247, 124, 198, 58, 216, 240, 4, 123, 34, 155, 91, 27, 232 };
    return new LicenciaService(storage, logger, publicKeyBytes);
});
builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<ICategoriaService, CategoriaService>();
builder.Services.AddScoped<IPresupuestoService, PresupuestoService>();
builder.Services.AddScoped<IGamificacionService, GamificacionService>();
builder.Services.AddScoped<ITiendaService, TiendaService>();
builder.Services.AddScoped<ISupabaseService, SupabaseService>();
// builder.Services.AddScoped<IHogarService, HogarService>(); // comentado — proximamente
builder.Services.AddScoped<IGastoService, GastoService>();
// builder.Services.AddScoped<ISyncService, SyncService>(); // comentado — proximamente
builder.Services.AddScoped<IRecurrenciaService, RecurrenciaService>();
builder.Services.AddScoped<INotificacionService, NotificacionService>();
builder.Services.AddScoped<IOnboardingService, OnboardingService>();
builder.Services.AddScoped<IPrivacyService, PrivacyService>();
builder.Services.AddScoped<IDataMigrationRunner, DataMigrationRunner>();
builder.Services.AddScoped<IDataMigration, V1SeedMigration>();
builder.Services.AddScoped<IDataMigration, V2NumeroVersionGastosMigration>();
builder.Services.AddScoped<IDataMigration, V3NumeroVersionEntidadesMigration>();
builder.Services.AddScoped<IDataMigration, V4SchemaVersionMigration>();
            builder.Services.AddScoped<IDataMigration, V5LimpiarReferenciasHuerfanasMigration>();
            builder.Services.AddScoped<IDataMigration, V7NormalizarDatosMigration>();
            builder.Services.AddScoped<IDataMigration, V8NombresMiembrosHogarMigration>();
builder.Services.AddScoped<IFinanciamientoService, FinanciamientoService>();
builder.Services.AddScoped<IReportCalculationService, ReportCalculationService>();
builder.Services.AddScoped<IFinanciamientoCalculationService, FinanciamientoCalculationService>();
// builder.Services.AddScoped<IAccountLifecycleService, AccountLifecycleService>(); // comentado — proximamente
builder.Services.AddScoped<IExportImportService, ExportImportService>();
builder.Services.AddScoped<IPinLockService, PinLockService>();
builder.Services.AddScoped<IStorageMonitorService, StorageMonitorService>();

builder.Services.AddSingleton<IConnectivityService, ConnectivityService>();

builder.Services.AddScoped<IndexedDbStorageService>();
builder.Services.AddScoped<IStorageService>(sp => sp.GetRequiredService<IndexedDbStorageService>());


var host = builder.Build();

try
{
    using var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
    var json = await http.GetStringAsync("appsettings.json");
    using var doc = System.Text.Json.JsonDocument.Parse(json);
    var supabase = doc.RootElement.GetProperty("Supabase");
    SupabaseConfig.Url = supabase.GetProperty("Url").GetString()!;
    SupabaseConfig.AnonKey = supabase.GetProperty("AnonKey").GetString()!;
}
catch (Exception ex)
{
    var log = host.Services.GetRequiredService<ILogger<Program>>();
    log.LogWarning(ex, "No se pudo cargar appsettings.json, usando valores por defecto");
}

var connectivity = host.Services.GetRequiredService<IConnectivityService>();
await connectivity.InitializeAsync();

var migrationRunner = host.Services.GetRequiredService<IDataMigrationRunner>();
var migrationResult = await migrationRunner.EjecutarMigrationsAsync();
var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Migraciones ejecutadas: {Result}", migrationResult);

var categoriaService = host.Services.GetRequiredService<ICategoriaService>();
await categoriaService.InicializarCategoriasPorDefectoAsync();

var recurrenciaService = host.Services.GetRequiredService<IRecurrenciaService>();
var generados = await recurrenciaService.GenerarPendientesAsync();
if (generados.Count > 0)
{
    logger.LogInformation("Recurrencias: {Count} gastos generados automáticamente", generados.Count);
}

var financiamientoService = host.Services.GetRequiredService<IFinanciamientoService>();
var cuotas = await financiamientoService.GenerarCuotasPendientesAsync();
if (cuotas.Count > 0)
{
    logger.LogInformation("Financiamientos: {Count} cuotas generadas automáticamente", cuotas.Count);
}

// Auto-sync comentado — proximamente
// try { ... }

await host.RunAsync();
