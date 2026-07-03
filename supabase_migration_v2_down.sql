-- Migration v2 DOWN: Revert licencia columns for Hogar Compartido
-- Run in Supabase SQL Editor

DROP INDEX IF EXISTS idx_hogares_token_hash;

ALTER TABLE hogares DROP COLUMN IF EXISTS plan_incluido;
ALTER TABLE hogares DROP COLUMN IF EXISTS modo_gamificado_incluido;
ALTER TABLE hogares DROP COLUMN IF EXISTS fecha_expiracion;
ALTER TABLE hogares DROP COLUMN IF EXISTS licencia_tipo;
ALTER TABLE hogares DROP COLUMN IF EXISTS token_hash;
