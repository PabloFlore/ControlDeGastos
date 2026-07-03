-- Migration v3: usuario_id columns for individual user data isolation
-- Run in Supabase SQL Editor

ALTER TABLE categorias ADD COLUMN IF NOT EXISTS usuario_id UUID;
ALTER TABLE presupuestos ADD COLUMN IF NOT EXISTS usuario_id UUID;

CREATE INDEX IF NOT EXISTS idx_categorias_usuario_id ON categorias (usuario_id);
CREATE INDEX IF NOT EXISTS idx_presupuestos_usuario_id ON presupuestos (usuario_id);

-- Nota: FOREIGN KEY a auth.users requiere que el proyecto Supabase tenga
-- la extensión citus o que la tabla auth.users exista en el mismo schema.
-- Si auth.users no está en public, descomenta y ajusta el schema:
-- ALTER TABLE categorias ADD CONSTRAINT fk_categorias_usuario FOREIGN KEY (usuario_id) REFERENCES auth.users(id) ON DELETE CASCADE;
-- ALTER TABLE presupuestos ADD CONSTRAINT fk_presupuestos_usuario FOREIGN KEY (usuario_id) REFERENCES auth.users(id) ON DELETE CASCADE;
