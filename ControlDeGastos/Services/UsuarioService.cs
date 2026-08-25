using System.Diagnostics;
using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class UsuarioService : IUsuarioService
{
    private const string StorageKey = "cdg_usuario";
    private readonly IStorageService _storage;
    private readonly ISupabaseService _supabase;
    private readonly ILogger<UsuarioService>? _logger;

    public UsuarioService(IStorageService storage, ISupabaseService supabase, ILogger<UsuarioService>? logger = null)
    {
        _storage = storage;
        _supabase = supabase;
        _logger = logger;
    }

    public async Task<Usuario> ObtenerUsuarioAsync()
    {
        var sw = Stopwatch.StartNew();
        _logger?.LogInformation("UsuarioService.ObtenerUsuarioAsync: INICIO");
        var usuario = await _storage.GetAsync<Usuario>(StorageKey);
        _logger?.LogInformation("UsuarioService.ObtenerUsuarioAsync: GetAsync {ms}ms, found={Found}", sw.ElapsedMilliseconds, usuario != null);
        if (usuario != null) return usuario;

        usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            Nombre = "Usuario",
            FechaRegistro = DateTime.UtcNow,
            PlanActivo = PlanType.Local,
            Moneda = "MXN"
        };

        await GuardarUsuarioAsync(usuario);
        _logger?.LogInformation("UsuarioService.ObtenerUsuarioAsync: TOTAL {ms}ms", sw.ElapsedMilliseconds);
        return usuario;
    }

    public async Task GuardarUsuarioAsync(Usuario usuario)
    {
        var sw = Stopwatch.StartNew();
        _logger?.LogInformation("UsuarioService.GuardarUsuarioAsync: INICIO");
        usuario.NumeroVersion++;
        await _storage.SetAsync(StorageKey, usuario);
        _logger?.LogInformation("UsuarioService.GuardarUsuarioAsync: SetAsync {ms}ms", sw.ElapsedMilliseconds);
    }

    public async Task SincronizarPerfilConNubeAsync()
    {
        var usuario = await ObtenerUsuarioAsync();
        if (usuario.PlanActivo != PlanType.Nube) return;

        try
        {
            var remotos = await _supabase.ObtenerTodosAsync<PerfilRecord>("perfiles", $"id=eq.{usuario.Id}");
            var perfil = new PerfilRecord
            {
                Id = usuario.Id,
                Nombre = usuario.Nombre,
                Moneda = usuario.Moneda,
                ModoGamificadoActivo = usuario.ModoGamificadoActivo,
                ExcluirRecurrentesDePresupuesto = usuario.ExcluirRecurrentesDePresupuesto,
                ExcluirCreditosDePresupuesto = usuario.ExcluirCreditosDePresupuesto,
                PinDelaySegundos = usuario.PinDelaySegundos
            };

            if (remotos.Count > 0)
            {
                var actualizado = await _supabase.ActualizarAsync("perfiles", usuario.Id, perfil);
                if (actualizado is not null)
                    usuario.UpdatedAt = actualizado.UpdatedAt;
            }
            else
            {
                var creado = await _supabase.GuardarAsync("perfiles", perfil);
                if (creado is not null)
                    usuario.UpdatedAt = creado.UpdatedAt;
            }

            usuario.Sincronizado = true;
            await _storage.SetAsync(StorageKey, usuario);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error al sincronizar perfil: {ex.Message}");
        }
    }

    public async Task<PerfilRecord?> ObtenerPerfilRemotoAsync()
    {
        var usuario = await ObtenerUsuarioAsync();
        if (usuario.PlanActivo != PlanType.Nube) return null;

        try
        {
            var remotos = await _supabase.ObtenerTodosAsync<PerfilRecord>("perfiles", $"id=eq.{usuario.Id}");
            return remotos.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public async Task CambiarPlanAsync(PlanType plan)
    {
        var usuario = await ObtenerUsuarioAsync();
        usuario.PlanActivo = plan;
        await GuardarUsuarioAsync(usuario);
    }

    public async Task CambiarModoGamificadoAsync(bool activo)
    {
        var usuario = await ObtenerUsuarioAsync();
        usuario.ModoGamificadoActivo = activo;
        await GuardarUsuarioAsync(usuario);
    }

    public async Task CambiarExcluirRecurrentesAsync(bool excluir)
    {
        var usuario = await ObtenerUsuarioAsync();
        usuario.ExcluirRecurrentesDePresupuesto = excluir;
        await GuardarUsuarioAsync(usuario);
    }

    public async Task CambiarExcluirCreditosAsync(bool excluir)
    {
        var usuario = await ObtenerUsuarioAsync();
        usuario.ExcluirCreditosDePresupuesto = excluir;
        await GuardarUsuarioAsync(usuario);
    }

    public async Task CambiarMostrarMinutosAsync(bool mostrar)
    {
        var usuario = await ObtenerUsuarioAsync();
        usuario.MostrarMinutos = mostrar;
        await GuardarUsuarioAsync(usuario);
    }

    public async Task CambiarMostrarGraficaIngresosAsync(bool mostrar)
    {
        var usuario = await ObtenerUsuarioAsync();
        usuario.MostrarGraficaIngresos = mostrar;
        await GuardarUsuarioAsync(usuario);
    }
}
