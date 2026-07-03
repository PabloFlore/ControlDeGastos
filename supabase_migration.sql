-- Migration: Hogar Compartido + Used Tokens
-- Run this in your Supabase SQL Editor

-- ============================================
-- 1. Tabla used_tokens (para licencias)
-- ============================================
CREATE TABLE IF NOT EXISTS used_tokens (
    token_hash TEXT PRIMARY KEY,
    activated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    token_type TEXT NOT NULL,
    plan TEXT NOT NULL,
    game BOOLEAN NOT NULL
);

ALTER TABLE used_tokens ENABLE ROW LEVEL SECURITY;

CREATE POLICY "anon_insert_used_tokens" ON used_tokens
    FOR INSERT TO anon
    WITH CHECK (true);

CREATE POLICY "anon_select_used_tokens" ON used_tokens
    FOR SELECT TO anon
    USING (true);

-- ============================================
-- 2. Tabla hogares
-- ============================================
CREATE TABLE IF NOT EXISTS hogares (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo_invitacion TEXT NOT NULL UNIQUE,
    creado_por_email TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

ALTER TABLE hogares ENABLE ROW LEVEL SECURITY;

CREATE POLICY "anon_select_hogares" ON hogares
    FOR SELECT TO anon
    USING (true);

CREATE POLICY "anon_insert_hogares" ON hogares
    FOR INSERT TO anon
    WITH CHECK (true);

-- ============================================
-- 3. Tabla hogar_miembros
-- ============================================
CREATE TABLE IF NOT EXISTS hogar_miembros (
    hogar_id UUID NOT NULL REFERENCES hogares(id) ON DELETE CASCADE,
    email TEXT NOT NULL,
    joined_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (hogar_id, email)
);

ALTER TABLE hogar_miembros ENABLE ROW LEVEL SECURITY;

CREATE POLICY "anon_select_hogar_miembros" ON hogar_miembros
    FOR SELECT TO anon
    USING (true);

CREATE POLICY "anon_insert_hogar_miembros" ON hogar_miembros
    FOR INSERT TO anon
    WITH CHECK (true);

CREATE POLICY "anon_delete_hogar_miembros" ON hogar_miembros
    FOR DELETE TO anon
    USING (true);

-- ============================================
-- 4. Agregar hogar_id a tablas existentes
-- ============================================
ALTER TABLE gastos ADD COLUMN IF NOT EXISTS hogar_id TEXT;
ALTER TABLE categorias ADD COLUMN IF NOT EXISTS hogar_id TEXT;
ALTER TABLE presupuestos ADD COLUMN IF NOT EXISTS hogar_id TEXT;
