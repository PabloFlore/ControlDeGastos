using ControlDeGastos.Models;

namespace ControlDeGastos.Services;

public class AccountLifecycleService : IAccountLifecycleService
{
    private readonly IUsuarioService _usuarioService;
    private readonly ISupabaseService _supabaseService;
    private readonly IGastoService _gastoService;
    private readonly IHogarService _hogarService;
    private readonly ILicenciaService _licenciaService;
    private readonly IStorageService _storage;
    private readonly ISyncService _syncService;
    private readonly IPresupuestoService _presupuestoService;
    private readonly ICategoriaService _categoriaService;
    private readonly IRecurrenciaService _recurrenciaService;
    private readonly IFinanciamientoService _financiamientoService;

    public AccountLifecycleService(
        IUsuarioService usuarioService,
        ISupabaseService supabaseService,
        IGastoService gastoService,
        IHogarService hogarService,
        ILicenciaService licenciaService,
        IStorageService storage,
        ISyncService syncService,
        IPresupuestoService presupuestoService,
        ICategoriaService categoriaService,
        IRecurrenciaService recurrenciaService,
        IFinanciamientoService financiamientoService)
    {
        _usuarioService = usuarioService;
        _supabaseService = supabaseService;
        _gastoService = gastoService;
        _hogarService = hogarService;
        _licenciaService = licenciaService;
        _storage = storage;
        _syncService = syncService;
        _presupuestoService = presupuestoService;
        _categoriaService = categoriaService;
        _recurrenciaService = recurrenciaService;
        _financiamientoService = financiamientoService;
    }

    public async Task<CloudConnectionResult> ConnectCloudAsync(string email, string password)
    {
        var ok = await _supabaseService.IniciarSesionAsync(email, password);
        if (!ok)
            return new CloudConnectionResult { Success = false, ErrorMessage = "No se pudo conectar a Supabase" };

        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null)
            return new CloudConnectionResult { Success = false, ErrorMessage = "Usuario no encontrado" };

        usuario.Email = email;
        usuario.PlanActivo = PlanType.Nube;
        var supabaseId = await _supabaseService.ObtenerUsuarioIdAsync();
        if (!string.IsNullOrEmpty(supabaseId))
            usuario.SupabaseUserId = supabaseId;
        await _usuarioService.GuardarUsuarioAsync(usuario);

        await _gastoService.MarcarTodosPendientesSyncAsync();

        return new CloudConnectionResult { Success = true };
    }

    public async Task<AccountResult> DisconnectCloudAsync()
    {
        await _supabaseService.CerrarSesionAsync();

        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is not null)
        {
            usuario.Email = null;
            usuario.HogarId = null;
            usuario.HogarCodigo = null;
            usuario.PlanActivo = PlanType.Local;
            await _usuarioService.GuardarUsuarioAsync(usuario);
        }

        return new AccountResult { Success = true };
    }

    public async Task<CloudConnectionResult> ReauthenticateCloudAsync(string email, string password)
    {
        var ok = await _supabaseService.IniciarSesionAsync(email, password);
        if (!ok)
            return new CloudConnectionResult { Success = false, ErrorMessage = "No se pudo conectar a Supabase" };

        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null)
            return new CloudConnectionResult { Success = false, ErrorMessage = "Usuario no encontrado" };

        usuario.Email = email;
        usuario.PlanActivo = PlanType.Nube;
        var supabaseId = await _supabaseService.ObtenerUsuarioIdAsync();
        if (!string.IsNullOrEmpty(supabaseId))
            usuario.SupabaseUserId = supabaseId;
        await _usuarioService.GuardarUsuarioAsync(usuario);

        await _gastoService.MarcarTodosPendientesSyncAsync();

        return new CloudConnectionResult { Success = true };
    }

    public async Task<AccountResult> LogoutCloudOnlyAsync()
    {
        await _supabaseService.CerrarSesionAsync();

        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is not null)
        {
            usuario.Email = null;
            usuario.HogarId = null;
            usuario.HogarCodigo = null;
            usuario.PlanActivo = PlanType.Local;
            await _usuarioService.GuardarUsuarioAsync(usuario);
        }

        return new AccountResult { Success = true };
    }

    public async Task<AccountResult> ToggleGamificationAsync(bool activate)
    {
        if (activate)
        {
            var licencia = await _licenciaService.ObtenerEstadoLicenciaAsync();
            if (!licencia.Valida || !licencia.ModoGamificadoIncluido)
                return new AccountResult { Success = false, ErrorMessage = "Tu licencia no incluye el modo RPG" };
        }

        await _usuarioService.CambiarModoGamificadoAsync(activate);

        return new AccountResult { Success = true };
    }

    public async Task<AccountResult> CreateHouseholdAsync()
    {
        var hogar = await _hogarService.CrearHogarAsync();

        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null)
            return new AccountResult { Success = false, ErrorMessage = "Usuario no encontrado" };

        usuario.HogarId = hogar.Id;
        usuario.HogarCodigo = hogar.CodigoInvitacion;
        await _usuarioService.GuardarUsuarioAsync(usuario);

        await MigrarDatosAHogarAsync(hogar.Id);
        await _syncService.SincronizarAhoraAsync();

        return new AccountResult { Success = true };
    }

    public async Task<AccountResult> JoinHouseholdAsync(string codigo, string email)
    {
        var codigoUpper = codigo.Trim().ToUpperInvariant();
        var ok = await _hogarService.UnirseAHogarAsync(codigoUpper, email);
        if (!ok)
            return new AccountResult { Success = false, ErrorMessage = "Código inválido o ya eres miembro" };

        var hogar = await _hogarService.ObtenerHogarPorCodigoAsync(codigoUpper);
        if (hogar is null)
            return new AccountResult { Success = false, ErrorMessage = "Hogar no encontrado" };

        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is null)
            return new AccountResult { Success = false, ErrorMessage = "Usuario no encontrado" };

        usuario.HogarId = hogar.Id;
        usuario.HogarCodigo = hogar.CodigoInvitacion;
        await _usuarioService.GuardarUsuarioAsync(usuario);

        await MigrarDatosAHogarAsync(hogar.Id);
        await _syncService.SincronizarAhoraAsync();

        return new AccountResult { Success = true };
    }

    private async Task MigrarDatosAHogarAsync(string hogarId)
    {
        await _gastoService.MigrarGastosAHogarAsync(hogarId);
        await _presupuestoService.MigrarPresupuestosAHogarAsync(hogarId);
        await _categoriaService.MigrarCategoriasAHogarAsync(hogarId);
        await _recurrenciaService.MigrarRecurrenciasAHogarAsync(hogarId);
        await _financiamientoService.MigrarFinanciamientosAHogarAsync(hogarId);
    }

    public async Task<AccountResult> LeaveHouseholdAsync(string hogarId, string email)
    {
        var ok = await _hogarService.SalirDelHogarAsync(hogarId, email);
        if (!ok)
            return new AccountResult { Success = false, ErrorMessage = "El creador del hogar no puede salir" };

        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        if (usuario is not null)
        {
            usuario.HogarId = null;
            usuario.HogarCodigo = null;
            await _usuarioService.GuardarUsuarioAsync(usuario);
        }

        return new AccountResult { Success = true };
    }

    public async Task<AccountResult> LogoutAndClearAsync()
    {
        await _supabaseService.CerrarSesionAsync();
        await LimpiarDatosLocalesAsync();
        return new AccountResult { Success = true };
    }

    public async Task LimpiarDatosLocalesAsync()
    {
        var cdgKeys = new[]
        {
            "cdg_licencia",
            "cdg_used_tokens",
            "cdg_usuario",
            "cdg_ultima_sync",
            "cdg_gastos",
            "cdg_categorias",
            "cdg_presupuestos",
            "cdg_recurrencias",
            "cdg_financiamientos",
            "cdg_onboarding",
            "cdg_notificaciones",
        };

        foreach (var key in cdgKeys)
        {
            if (await _storage.KeyExistsAsync(key))
                await _storage.RemoveAsync(key);
        }
    }
}
