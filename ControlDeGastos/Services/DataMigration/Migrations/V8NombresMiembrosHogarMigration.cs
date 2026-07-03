using ControlDeGastos.Models;

namespace ControlDeGastos.Services.DataMigration.Migrations;

public class V8NombresMiembrosHogarMigration : IDataMigration
{
    public int Version => 8;
    public string Descripcion => "Agrega nombre, avatar, color y usuario_id a miembros del hogar para mostrar quién registró cada gasto";

    public async Task<bool> MigrateAsync(IStorageService storage)
    {
        var modificados = false;

        // Migrar miembros del hogar local
        var miembros = await storage.GetAsync<List<HogarMiembro>>("cdg_hogar_miembros");
        if (miembros is not null && miembros.Count > 0)
        {
            var usuario = await storage.GetAsync<Usuario>("cdg_usuario");
            var usuarioNombre = usuario?.Nombre ?? "Usuario";
            var usuarioId = usuario?.Id;

            foreach (var m in miembros)
            {
                if (string.IsNullOrEmpty(m.Nombre))
                {
                    m.Nombre = usuarioNombre;
                    modificados = true;
                }
                if (string.IsNullOrEmpty(m.Avatar))
                {
                    m.Avatar = "👤";
                    modificados = true;
                }
                if (string.IsNullOrEmpty(m.Color))
                {
                    m.Color = GenerarColor();
                    modificados = true;
                }
                if (m.UsuarioId == null && usuarioId.HasValue)
                {
                    m.UsuarioId = usuarioId.Value;
                    modificados = true;
                }
            }

            if (modificados)
                await storage.SetAsync("cdg_hogar_miembros", miembros);
        }

        return modificados;
    }

    private string GenerarColor()
    {
        var colores = new[] { "#E91E63", "#9C27B0", "#673AB7", "#3F51B5", "#2196F3", "#00BCD4", "#009688", "#4CAF50", "#8BC34A", "#FF9800", "#FF5722", "#795548" };
        return colores[Random.Shared.Next(colores.Length)];
    }
}