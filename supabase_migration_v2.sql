-- Migration v2: Licencia columns for Hogar Compartido
-- Run in Supabase SQL Editor

ALTER TABLE hogares ADD COLUMN IF NOT EXISTS token_hash TEXT;
ALTER TABLE hogares ADD COLUMN IF NOT EXISTS licencia_tipo TEXT NOT NULL DEFAULT 'Trial';
ALTER TABLE hogares ADD COLUMN IF NOT EXISTS fecha_expiracion TIMESTAMPTZ;
ALTER TABLE hogares ADD COLUMN IF NOT EXISTS modo_gamificado_incluido BOOLEAN NOT NULL DEFAULT false;
ALTER TABLE hogares ADD COLUMN IF NOT EXISTS plan_incluido TEXT NOT NULL DEFAULT 'NUBE';

-- Evita duplicados de token_hash (cada licencia debe ser única)
CREATE UNIQUE INDEX IF NOT EXISTS idx_hogares_token_hash ON hogares (token_hash) WHERE token_hash IS NOT NULL;
