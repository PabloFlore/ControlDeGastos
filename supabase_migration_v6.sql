-- Migration v6: Suscripciones table for subscriptions
-- Run this in your Supabase SQL Editor AFTER migrations v1-v5
-- ================================================================

-- ============================================
-- 1. Create suscripciones table
-- ============================================
CREATE TABLE IF NOT EXISTS suscripciones (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    nombre TEXT NOT NULL,
    categoria_id UUID,
    monto DECIMAL(12,2) NOT NULL DEFAULT 0,
    periodicidad TEXT NOT NULL DEFAULT 'Mensual',
    fecha_inicio TIMESTAMPTZ DEFAULT NOW(),
    fecha_fin TIMESTAMPTZ,
    proximo_pago TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    activa BOOLEAN DEFAULT true,
    hogar_id TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    sincronizado BOOLEAN DEFAULT false,
    numero_version INT DEFAULT 1,
    schema_version INT DEFAULT 0
);

-- ============================================
-- 2. Indexes
-- ============================================
CREATE INDEX IF NOT EXISTS idx_suscripciones_usuario_id ON suscripciones (usuario_id);
CREATE INDEX IF NOT EXISTS idx_suscripciones_hogar_id ON suscripciones (hogar_id);
CREATE INDEX IF NOT EXISTS idx_suscripciones_updated_at ON suscripciones (updated_at);

-- ============================================
-- 3. Enable RLS
-- ============================================
ALTER TABLE suscripciones ENABLE ROW LEVEL SECURITY;

-- ============================================
-- 4. RLS Policies
-- ============================================
CREATE POLICY "suscripciones_select_own" ON suscripciones
    FOR SELECT TO authenticated
    USING (usuario_id = auth.uid());

CREATE POLICY "suscripciones_insert_own" ON suscripciones
    FOR INSERT TO authenticated
    WITH CHECK (usuario_id = auth.uid());

CREATE POLICY "suscripciones_update_own" ON suscripciones
    FOR UPDATE TO authenticated
    USING (usuario_id = auth.uid())
    WITH CHECK (usuario_id = auth.uid());

CREATE POLICY "suscripciones_delete_own" ON suscripciones
    FOR DELETE TO authenticated
    USING (usuario_id = auth.uid());

-- ============================================
-- 5. Updated_at trigger
-- ============================================
CREATE OR REPLACE FUNCTION trg_suscripciones_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_suscripciones_updated_at ON suscripciones;
CREATE TRIGGER trg_suscripciones_updated_at
    BEFORE UPDATE ON suscripciones
    FOR EACH ROW
    EXECUTE FUNCTION trg_suscripciones_updated_at();
