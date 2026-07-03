-- Migration v3 DOWN: Revert usuario_id columns and indexes
-- Run in Supabase SQL Editor

DROP INDEX IF EXISTS idx_presupuestos_usuario_id;
DROP INDEX IF EXISTS idx_categorias_usuario_id;

ALTER TABLE presupuestos DROP COLUMN IF EXISTS usuario_id;
ALTER TABLE categorias DROP COLUMN IF EXISTS usuario_id;
