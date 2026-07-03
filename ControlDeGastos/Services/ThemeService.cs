namespace ControlDeGastos.Services;

public class ThemeService : IThemeService
{
    private readonly IUsuarioService _usuarioService;

    public ThemeService(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    public async Task<string> ObtenerClaseBodyAsync()
    {
        var usuario = await _usuarioService.ObtenerUsuarioAsync();
        return usuario.ModoGamificadoActivo ? "rpg-theme" : "";
    }
}
