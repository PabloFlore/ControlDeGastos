-- Migration v4: RLS Policies + Foreign Keys + Server-side updated_at triggers
-- Run this in your Supabase SQL Editor AFTER migrations v1, v2, v3
-- ================================================================

-- ============================================
-- 1. Add missing usuario_id columns
-- ============================================
ALTER TABLE gastos ADD COLUMN IF NOT EXISTS usuario_id UUID;
ALTER TABLE recurrencias ADD COLUMN IF NOT EXISTS usuario_id UUID;
ALTER TABLE financiamientos ADD COLUMN IF NOT EXISTS usuario_id UUID;

-- ============================================
-- 2. Add updated_at column for server-side timestamps
-- ============================================
ALTER TABLE gastos ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ;
ALTER TABLE categorias ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ;
ALTER TABLE presupuestos ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ;
ALTER TABLE recurrencias ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ;
ALTER TABLE financiamientos ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ;

-- ============================================
-- 3. Indexes on usuario_id for performance
-- ============================================
CREATE INDEX IF NOT EXISTS idx_gastos_usuario_id ON gastos (usuario_id);
CREATE INDEX IF NOT EXISTS idx_recurrencias_usuario_id ON recurrencias (usuario_id);
CREATE INDEX IF NOT EXISTS idx_financiamientos_usuario_id ON financiamientos (usuario_id);
CREATE INDEX IF NOT EXISTS idx_gastos_updated_at ON gastos (updated_at);
CREATE INDEX IF NOT EXISTS idx_categorias_updated_at ON categorias (updated_at);
CREATE INDEX IF NOT EXISTS idx_presupuestos_updated_at ON presupuestos (updated_at);
CREATE INDEX IF NOT EXISTS idx_recurrencias_updated_at ON recurrencias (updated_at);
CREATE INDEX IF NOT EXISTS idx_financiamientos_updated_at ON financiamientos (updated_at);

-- ============================================
-- 4. Server-side updated_at trigger function
-- ============================================
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- ============================================
-- 5. Apply triggers to all data tables
-- ============================================
DROP TRIGGER IF EXISTS trg_gastos_updated_at ON gastos;
CREATE TRIGGER trg_gastos_updated_at
    BEFORE UPDATE ON gastos
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS trg_categorias_updated_at ON categorias;
CREATE TRIGGER trg_categorias_updated_at
    BEFORE UPDATE ON categorias
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS trg_presupuestos_updated_at ON presupuestos;
CREATE TRIGGER trg_presupuestos_updated_at
    BEFORE UPDATE ON presupuestos
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS trg_recurrencias_updated_at ON recurrencias;
CREATE TRIGGER trg_recurrencias_updated_at
    BEFORE UPDATE ON recurrencias
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS trg_financiamientos_updated_at ON financiamientos;
CREATE TRIGGER trg_financiamientos_updated_at
    BEFORE UPDATE ON financiamientos
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- ============================================
-- 6. Foreign Keys to auth.users
-- ============================================
-- Each table's usuario_id references auth.users(id)
ALTER TABLE gastos DROP CONSTRAINT IF EXISTS fk_gastos_usuario;
ALTER TABLE gastos ADD CONSTRAINT fk_gastos_usuario
    FOREIGN KEY (usuario_id) REFERENCES auth.users(id) ON DELETE CASCADE;

ALTER TABLE categorias DROP CONSTRAINT IF EXISTS fk_categorias_usuario;
ALTER TABLE categorias ADD CONSTRAINT fk_categorias_usuario
    FOREIGN KEY (usuario_id) REFERENCES auth.users(id) ON DELETE CASCADE;

ALTER TABLE presupuestos DROP CONSTRAINT IF EXISTS fk_presupuestos_usuario;
ALTER TABLE presupuestos ADD CONSTRAINT fk_presupuestos_usuario
    FOREIGN KEY (usuario_id) REFERENCES auth.users(id) ON DELETE CASCADE;

ALTER TABLE recurrencias DROP CONSTRAINT IF EXISTS fk_recurrencias_usuario;
ALTER TABLE recurrencias ADD CONSTRAINT fk_recurrencias_usuario
    FOREIGN KEY (usuario_id) REFERENCES auth.users(id) ON DELETE CASCADE;

ALTER TABLE financiamientos DROP CONSTRAINT IF EXISTS fk_financiamientos_usuario;
ALTER TABLE financiamientos ADD CONSTRAINT fk_financiamientos_usuario
    FOREIGN KEY (usuario_id) REFERENCES auth.users(id) ON DELETE CASCADE;

-- ============================================
-- 7. RLS Policies — Replace anon_* with authenticated per-user policies
-- ============================================

-- 7a. Drop old insecure anon policies
DROP POLICY IF EXISTS "anon_select_used_tokens" ON used_tokens;
DROP POLICY IF EXISTS "anon_insert_used_tokens" ON used_tokens;
DROP POLICY IF EXISTS "anon_select_hogares" ON hogares;
DROP POLICY IF EXISTS "anon_insert_hogares" ON hogares;
DROP POLICY IF EXISTS "anon_select_hogar_miembros" ON hogar_miembros;
DROP POLICY IF EXISTS "anon_insert_hogar_miembros" ON hogar_miembros;
DROP POLICY IF EXISTS "anon_delete_hogar_miembros" ON hogar_miembros;

-- 7b. used_tokens — any authenticated user can read/insert (tokens are one-time-use)
CREATE POLICY "auth_select_used_tokens" ON used_tokens
    FOR SELECT TO authenticated
    USING (true);

CREATE POLICY "auth_insert_used_tokens" ON used_tokens
    FOR INSERT TO authenticated
    WITH CHECK (true);

-- 7c. hogares — members can read; creator can insert/update
CREATE POLICY "auth_select_hogares" ON hogares
    FOR SELECT TO authenticated
    USING (
        creado_por_email = auth.email() OR
        id IN (SELECT hogar_id FROM hogar_miembros WHERE email = auth.email())
    );

CREATE POLICY "auth_insert_hogares" ON hogares
    FOR INSERT TO authenticated
    WITH CHECK (creado_por_email = auth.email());

CREATE POLICY "auth_update_hogares" ON hogares
    FOR UPDATE TO authenticated
    USING (creado_por_email = auth.email())
    WITH CHECK (creado_por_email = auth.email());

-- 7d. hogar_miembros — members can read; user can insert self; creator can delete
CREATE POLICY "auth_select_hogar_miembros" ON hogar_miembros
    FOR SELECT TO authenticated
    USING (
        email = auth.email() OR
        hogar_id IN (SELECT hogar_id FROM hogar_miembros WHERE email = auth.email())
    );

CREATE POLICY "auth_insert_hogar_miembros" ON hogar_miembros
    FOR INSERT TO authenticated
    WITH CHECK (email = auth.email() OR EXISTS (
        SELECT 1 FROM hogares WHERE id = hogar_id AND creado_por_email = auth.email()
    ));

CREATE POLICY "auth_delete_hogar_miembros" ON hogar_miembros
    FOR DELETE TO authenticated
    USING (email = auth.email() OR EXISTS (
        SELECT 1 FROM hogares WHERE id = hogar_id AND creado_por_email = auth.email()
    ));

-- 7e. gastos — per-user and per-household policies
ALTER TABLE gastos ENABLE ROW LEVEL SECURITY;

CREATE POLICY "gastos_select" ON gastos
    FOR SELECT TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

CREATE POLICY "gastos_insert" ON gastos
    FOR INSERT TO authenticated
    WITH CHECK (usuario_id = auth.uid());

CREATE POLICY "gastos_update" ON gastos
    FOR UPDATE TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

CREATE POLICY "gastos_delete" ON gastos
    FOR DELETE TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

-- 7f. categorias — per-user and per-household policies
ALTER TABLE categorias ENABLE ROW LEVEL SECURITY;

CREATE POLICY "categorias_select" ON categorias
    FOR SELECT TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

CREATE POLICY "categorias_insert" ON categorias
    FOR INSERT TO authenticated
    WITH CHECK (usuario_id = auth.uid());

CREATE POLICY "categorias_update" ON categorias
    FOR UPDATE TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

CREATE POLICY "categorias_delete" ON categorias
    FOR DELETE TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

-- 7g. presupuestos — per-user and per-household policies
ALTER TABLE presupuestos ENABLE ROW LEVEL SECURITY;

CREATE POLICY "presupuestos_select" ON presupuestos
    FOR SELECT TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

CREATE POLICY "presupuestos_insert" ON presupuestos
    FOR INSERT TO authenticated
    WITH CHECK (usuario_id = auth.uid());

CREATE POLICY "presupuestos_update" ON presupuestos
    FOR UPDATE TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

CREATE POLICY "presupuestos_delete" ON presupuestos
    FOR DELETE TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

-- 7h. recurrencias — per-user and per-household policies
ALTER TABLE recurrencias ENABLE ROW LEVEL SECURITY;

CREATE POLICY "recurrencias_select" ON recurrencias
    FOR SELECT TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

CREATE POLICY "recurrencias_insert" ON recurrencias
    FOR INSERT TO authenticated
    WITH CHECK (usuario_id = auth.uid());

CREATE POLICY "recurrencias_update" ON recurrencias
    FOR UPDATE TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

CREATE POLICY "recurrencias_delete" ON recurrencias
    FOR DELETE TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

-- 7i. financiamientos — per-user and per-household policies
ALTER TABLE financiamientos ENABLE ROW LEVEL SECURITY;

CREATE POLICY "financiamientos_select" ON financiamientos
    FOR SELECT TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

CREATE POLICY "financiamientos_insert" ON financiamientos
    FOR INSERT TO authenticated
    WITH CHECK (usuario_id = auth.uid());

CREATE POLICY "financiamientos_update" ON financiamientos
    FOR UPDATE TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

CREATE POLICY "financiamientos_delete" ON financiamientos
    FOR DELETE TO authenticated
    USING (
        usuario_id = auth.uid() OR
        hogar_id IN (SELECT hogar_id::text FROM hogar_miembros WHERE email = auth.email())
    );

-- ============================================
-- 8. Ensure RLS is enabled on all tables
-- ============================================
ALTER TABLE used_tokens ENABLE ROW LEVEL SECURITY;
ALTER TABLE hogares ENABLE ROW LEVEL SECURITY;
ALTER TABLE hogar_miembros ENABLE ROW LEVEL SECURITY;
