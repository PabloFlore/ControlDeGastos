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
    var http = sp.GetRequiredService<HttpClient>();
    var publicKeyBytes = new byte[] { 48, 89, 48, 19, 6, 7, 42, 134, 72, 206, 61, 2, 1, 6, 8, 42, 134, 72, 206, 61, 3, 1, 7, 3, 66, 0, 4, 45, 85, 9, 191, 65, 250, 60, 109, 6, 28, 92, 118, 29, 115, 120, 180, 222, 98, 69, 153, 190, 35, 114, 26, 187, 229, 200, 93, 96, 108, 141, 160, 42, 215, 192, 186, 4, 77, 215, 122, 96, 53, 228, 108, 169, 44, 239, 203, 200, 246, 106, 0, 158, 21, 135, 31, 245, 3, 48, 149, 105, 61, 61, 164 };
    return new LicenciaService(storage, logger, publicKeyBytes, http);
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
builder.Services.AddScoped<IUpdateService, UpdateService>();

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
