-- Migración v8: Agregar nombre, avatar, color y usuario_id a hogar_miembros
-- Para mostrar quién registró cada gasto en hogares compartidos

ALTER TABLE hogar_miembros 
ADD COLUMN IF NOT EXISTS nombre TEXT,
ADD COLUMN IF NOT EXISTS avatar TEXT,
ADD COLUMN IF NOT EXISTS color TEXT,
ADD COLUMN IF NOT EXISTS usuario_id UUID;

-- Poblar miembros existentes con datos de usuarios
UPDATE hogar_miembros hm
SET 
    nombre = COALESCE(u.nombre, split_part(hm.email, '@', 1)),
    avatar = '👤',
    color = (
        SELECT color FROM (
            VALUES 
                ('#E91E63'), ('#9C27B0'), ('#673AB7'), ('#3F51B5'), 
                ('#2196F3'), ('#00BCD4'), ('#009688'), ('#4CAF50'), 
                ('#8BC34A'), ('#FF9800'), ('#FF5722'), ('#795548')
        ) AS c(color)
        WHERE hm.id IS NOT NULL
        ORDER BY hashtext(hm.email) LIMIT 1
    ),
    usuario_id = u.id
FROM usuarios u 
WHERE hm.email = u.email 
AND (hm.nombre IS NULL OR hm.nombre = '');

-- Índice para búsquedas por usuario_id
CREATE INDEX IF NOT EXISTS idx_hogar_miembros_usuario_id ON hogar_miembros(usuario_id);

-- Comentario
COMMENT ON COLUMN hogar_miembros.nombre IS 'Nombre visible del miembro en el hogar';
COMMENT ON COLUMN hogar_miembros.avatar IS 'Emoji o identificador visual del miembro';
COMMENT ON COLUMN hogar_miembros.color IS 'Color hex para badge del miembro';
COMMENT ON COLUMN hogar_miembros.usuario_id IS 'Referencia al usuario propietario (para join directo)';