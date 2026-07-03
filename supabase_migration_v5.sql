-- Migration v5: Perfiles table for user profile sync
-- Run this in your Supabase SQL Editor AFTER migrations v1-v4
-- ================================================================

-- ============================================
-- 1. Create perfiles table
-- ============================================
CREATE TABLE IF NOT EXISTS perfiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    nombre TEXT,
    moneda TEXT DEFAULT 'MXN',
    modo_gamificado_activo BOOLEAN DEFAULT false,
    excluir_recurrentes_de_presupuesto BOOLEAN DEFAULT false,
    excluir_creditos_de_presupuesto BOOLEAN DEFAULT false,
    pin_delay_segundos INT DEFAULT 30,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- ============================================
-- 2. Indexes
-- ============================================
CREATE INDEX IF NOT EXISTS idx_perfiles_usuario_id ON perfiles (usuario_id);
CREATE INDEX IF NOT EXISTS idx_perfiles_updated_at ON perfiles (updated_at);

-- ============================================
-- 3. Enable RLS
-- ============================================
ALTER TABLE perfiles ENABLE ROW LEVEL SECURITY;

-- ============================================
-- 4. RLS Policies
-- ============================================
CREATE POLICY "perfiles_select_own" ON perfiles
    FOR SELECT TO authenticated
    USING (usuario_id = auth.uid());

CREATE POLICY "perfiles_insert_own" ON perfiles
    FOR INSERT TO authenticated
    WITH CHECK (usuario_id = auth.uid());

CREATE POLICY "perfiles_update_own" ON perfiles
    FOR UPDATE TO authenticated
    USING (usuario_id = auth.uid())
    WITH CHECK (usuario_id = auth.uid());

CREATE POLICY "perfiles_delete_own" ON perfiles
    FOR DELETE TO authenticated
    USING (usuario_id = auth.uid());

-- ============================================
-- 5. Updated_at trigger
-- ============================================
CREATE OR REPLACE FUNCTION trg_perfiles_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_perfiles_updated_at ON perfiles;
CREATE TRIGGER trg_perfiles_updated_at
    BEFORE UPDATE ON perfiles
    FOR EACH ROW
    EXECUTE FUNCTION trg_perfiles_updated_at();
