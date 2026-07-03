-- ============================================
-- Migración v12: Crear tablas de datos faltantes + políticas anon
-- ============================================
-- Crea gastos, categorias, presupuestos, recurrencias, financiamientos
-- si no existen, y agrega políticas anon para todas las tablas de datos.
-- ============================================

-- ============================================
-- 1. gastos
-- ============================================
CREATE TABLE IF NOT EXISTS gastos (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id UUID NOT NULL,
    categoria_id UUID NOT NULL,
    monto NUMERIC NOT NULL,
    descripcion TEXT,
    fecha TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    recurrencia_id UUID,
    financiamiento_id UUID,
    es_gasto_compartido BOOLEAN DEFAULT false,
    creado_en TIMESTAMPTZ DEFAULT NOW(),
    actualizado_en TIMESTAMPTZ,
    hogar_id TEXT,
    sincronizado BOOLEAN DEFAULT false,
    updated_at TIMESTAMPTZ,
    numero_version INTEGER DEFAULT 1,
    schema_version INTEGER DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_gastos_usuario_id ON gastos (usuario_id);
CREATE INDEX IF NOT EXISTS idx_gastos_hogar_id ON gastos (hogar_id);
CREATE INDEX IF NOT EXISTS idx_gastos_fecha ON gastos (fecha);
CREATE INDEX IF NOT EXISTS idx_gastos_updated_at ON gastos (updated_at);

ALTER TABLE gastos ENABLE ROW LEVEL SECURITY;

-- ============================================
-- 2. categorias
-- ============================================
CREATE TABLE IF NOT EXISTS categorias (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id UUID,
    nombre TEXT NOT NULL,
    icono TEXT DEFAULT '📁',
    color TEXT DEFAULT '#6c757d',
    tipo TEXT DEFAULT 'Gasto',
    orden INTEGER DEFAULT 0,
    presupuesto_por_defecto NUMERIC,
    es_personalizada BOOLEAN DEFAULT false,
    hogar_id TEXT,
    actualizado_en TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    numero_version INTEGER DEFAULT 1,
    schema_version INTEGER DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_categorias_usuario_id ON categorias (usuario_id);
CREATE INDEX IF NOT EXISTS idx_categorias_hogar_id ON categorias (hogar_id);
CREATE INDEX IF NOT EXISTS idx_categorias_updated_at ON categorias (updated_at);

ALTER TABLE categorias ENABLE ROW LEVEL SECURITY;

-- ============================================
-- 3. presupuestos
-- ============================================
CREATE TABLE IF NOT EXISTS presupuestos (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id UUID NOT NULL,
    categoria_id UUID,
    monto_limite NUMERIC NOT NULL,
    periodo TEXT DEFAULT 'Mensual',
    fecha_inicio TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    fecha_fin TIMESTAMPTZ,
    creado_en TIMESTAMPTZ DEFAULT NOW(),
    actualizado_en TIMESTAMPTZ DEFAULT NOW(),
    hogar_id TEXT,
    updated_at TIMESTAMPTZ,
    numero_version INTEGER DEFAULT 1,
    schema_version INTEGER DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_presupuestos_usuario_id ON presupuestos (usuario_id);
CREATE INDEX IF NOT EXISTS idx_presupuestos_hogar_id ON presupuestos (hogar_id);
CREATE INDEX IF NOT EXISTS idx_presupuestos_updated_at ON presupuestos (updated_at);

ALTER TABLE presupuestos ENABLE ROW LEVEL SECURITY;

-- ============================================
-- 4. recurrencias
-- ============================================
CREATE TABLE IF NOT EXISTS recurrencias (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id UUID NOT NULL,
    categoria_id UUID,
    monto NUMERIC NOT NULL,
    descripcion TEXT,
    tipo_recurrencia TEXT DEFAULT 'Mensual',
    fecha_inicio TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    fecha_fin TIMESTAMPTZ,
    proxima_fecha TIMESTAMPTZ NOT NULL,
    activa BOOLEAN DEFAULT true,
    intervalo INTEGER DEFAULT 1,
    subscription_id UUID,
    hogar_id TEXT,
    creado_en TIMESTAMPTZ DEFAULT NOW(),
    actualizado_en TIMESTAMPTZ DEFAULT NOW(),
    sincronizado BOOLEAN DEFAULT false,
    updated_at TIMESTAMPTZ,
    numero_version INTEGER DEFAULT 1,
    schema_version INTEGER DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_recurrencias_usuario_id ON recurrencias (usuario_id);
CREATE INDEX IF NOT EXISTS idx_recurrencias_hogar_id ON recurrencias (hogar_id);
CREATE INDEX IF NOT EXISTS idx_recurrencias_updated_at ON recurrencias (updated_at);

ALTER TABLE recurrencias ENABLE ROW LEVEL SECURITY;

-- ============================================
-- 5. financiamientos
-- ============================================
CREATE TABLE IF NOT EXISTS financiamientos (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id UUID NOT NULL,
    tipo TEXT DEFAULT 'Credito',
    banco TEXT NOT NULL DEFAULT '',
    alias TEXT NOT NULL DEFAULT '',
    monto_total NUMERIC NOT NULL,
    plazo_meses INTEGER NOT NULL,
    tasa_interes_anual NUMERIC,
    fecha_inicio TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    activo BOOLEAN DEFAULT true,
    categoria_id UUID,
    creado_en TIMESTAMPTZ DEFAULT NOW(),
    actualizado_en TIMESTAMPTZ,
    hogar_id TEXT,
    sincronizado BOOLEAN DEFAULT false,
    updated_at TIMESTAMPTZ,
    numero_version INTEGER DEFAULT 1,
    schema_version INTEGER DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_financiamientos_usuario_id ON financiamientos (usuario_id);
CREATE INDEX IF NOT EXISTS idx_financiamientos_hogar_id ON financiamientos (hogar_id);
CREATE INDEX IF NOT EXISTS idx_financiamientos_updated_at ON financiamientos (updated_at);

ALTER TABLE financiamientos ENABLE ROW LEVEL SECURITY;

-- ============================================
-- 6. suscripciones (si no existe ya por v6)
-- ============================================
CREATE TABLE IF NOT EXISTS suscripciones (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id UUID NOT NULL,
    nombre TEXT NOT NULL,
    categoria_id UUID,
    monto NUMERIC NOT NULL,
    periodicidad TEXT DEFAULT 'Mensual',
    fecha_inicio TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    fecha_fin TIMESTAMPTZ,
    proximo_pago TIMESTAMPTZ NOT NULL,
    activa BOOLEAN DEFAULT true,
    hogar_id TEXT,
    creado_en TIMESTAMPTZ DEFAULT NOW(),
    actualizado_en TIMESTAMPTZ,
    sincronizado BOOLEAN DEFAULT false,
    updated_at TIMESTAMPTZ,
    numero_version INTEGER DEFAULT 1,
    schema_version INTEGER DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_suscripciones_usuario_id ON suscripciones (usuario_id);
CREATE INDEX IF NOT EXISTS idx_suscripciones_hogar_id ON suscripciones (hogar_id);
CREATE INDEX IF NOT EXISTS idx_suscripciones_updated_at ON suscripciones (updated_at);

ALTER TABLE suscripciones ENABLE ROW LEVEL SECURITY;

-- ============================================
-- 7. perfiles (si no existe ya por v5)
-- ============================================
CREATE TABLE IF NOT EXISTS perfiles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    nombre TEXT,
    moneda TEXT DEFAULT 'MXN',
    modo_gamificado_activo BOOLEAN DEFAULT false,
    excluir_recurrentes_de_presupuesto BOOLEAN DEFAULT false,
    excluir_creditos_de_presupuesto BOOLEAN DEFAULT false,
    pin_delay_segundos INTEGER DEFAULT 30,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_perfiles_usuario_id ON perfiles (usuario_id);
CREATE INDEX IF NOT EXISTS idx_perfiles_updated_at ON perfiles (updated_at);

ALTER TABLE perfiles ENABLE ROW LEVEL SECURITY;

-- ============================================
-- 8. Políticas anon para todas las tablas
-- ============================================

-- gastos
CREATE POLICY "anon_select_gastos" ON gastos
    FOR SELECT TO anon
    USING (true);

CREATE POLICY "anon_insert_gastos" ON gastos
    FOR INSERT TO anon
    WITH CHECK (true);

CREATE POLICY "anon_update_gastos" ON gastos
    FOR UPDATE TO anon
    USING (true)
    WITH CHECK (true);

CREATE POLICY "anon_delete_gastos" ON gastos
    FOR DELETE TO anon
    USING (true);

-- categorias
CREATE POLICY "anon_select_categorias" ON categorias
    FOR SELECT TO anon
    USING (true);

CREATE POLICY "anon_insert_categorias" ON categorias
    FOR INSERT TO anon
    WITH CHECK (true);

CREATE POLICY "anon_update_categorias" ON categorias
    FOR UPDATE TO anon
    USING (true)
    WITH CHECK (true);

CREATE POLICY "anon_delete_categorias" ON categorias
    FOR DELETE TO anon
    USING (true);

-- presupuestos
CREATE POLICY "anon_select_presupuestos" ON presupuestos
    FOR SELECT TO anon
    USING (true);

CREATE POLICY "anon_insert_presupuestos" ON presupuestos
    FOR INSERT TO anon
    WITH CHECK (true);

CREATE POLICY "anon_update_presupuestos" ON presupuestos
    FOR UPDATE TO anon
    USING (true)
    WITH CHECK (true);

CREATE POLICY "anon_delete_presupuestos" ON presupuestos
    FOR DELETE TO anon
    USING (true);

-- recurrencias
CREATE POLICY "anon_select_recurrencias" ON recurrencias
    FOR SELECT TO anon
    USING (true);

CREATE POLICY "anon_insert_recurrencias" ON recurrencias
    FOR INSERT TO anon
    WITH CHECK (true);

CREATE POLICY "anon_update_recurrencias" ON recurrencias
    FOR UPDATE TO anon
    USING (true)
    WITH CHECK (true);

CREATE POLICY "anon_delete_recurrencias" ON recurrencias
    FOR DELETE TO anon
    USING (true);

-- financiamientos
CREATE POLICY "anon_select_financiamientos" ON financiamientos
    FOR SELECT TO anon
    USING (true);

CREATE POLICY "anon_insert_financiamientos" ON financiamientos
    FOR INSERT TO anon
    WITH CHECK (true);

CREATE POLICY "anon_update_financiamientos" ON financiamientos
    FOR UPDATE TO anon
    USING (true)
    WITH CHECK (true);

CREATE POLICY "anon_delete_financiamientos" ON financiamientos
    FOR DELETE TO anon
    USING (true);

-- suscripciones
CREATE POLICY "anon_select_suscripciones" ON suscripciones
    FOR SELECT TO anon
    USING (true);

CREATE POLICY "anon_insert_suscripciones" ON suscripciones
    FOR INSERT TO anon
    WITH CHECK (true);

CREATE POLICY "anon_update_suscripciones" ON suscripciones
    FOR UPDATE TO anon
    USING (true)
    WITH CHECK (true);

CREATE POLICY "anon_delete_suscripciones" ON suscripciones
    FOR DELETE TO anon
    USING (true);

-- perfiles
CREATE POLICY "anon_select_perfiles" ON perfiles
    FOR SELECT TO anon
    USING (true);

CREATE POLICY "anon_insert_perfiles" ON perfiles
    FOR INSERT TO anon
    WITH CHECK (true);

CREATE POLICY "anon_update_perfiles" ON perfiles
    FOR UPDATE TO anon
    USING (true)
    WITH CHECK (true);

CREATE POLICY "anon_delete_perfiles" ON perfiles
    FOR DELETE TO anon
    USING (true);
