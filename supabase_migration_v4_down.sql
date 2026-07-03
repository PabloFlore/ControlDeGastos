-- Migration v4 DOWN: Revert RLS + FKs + Triggers
-- Run in Supabase SQL Editor
-- WARNING: This will drop RLS policies, foreign keys, and triggers

-- ============================================
-- 1. Drop RLS policies for all tables
-- ============================================
DROP POLICY IF EXISTS "gastos_select" ON gastos;
DROP POLICY IF EXISTS "gastos_insert" ON gastos;
DROP POLICY IF EXISTS "gastos_update" ON gastos;
DROP POLICY IF EXISTS "gastos_delete" ON gastos;

DROP POLICY IF EXISTS "categorias_select" ON categorias;
DROP POLICY IF EXISTS "categorias_insert" ON categorias;
DROP POLICY IF EXISTS "categorias_update" ON categorias;
DROP POLICY IF EXISTS "categorias_delete" ON categorias;

DROP POLICY IF EXISTS "presupuestos_select" ON presupuestos;
DROP POLICY IF EXISTS "presupuestos_insert" ON presupuestos;
DROP POLICY IF EXISTS "presupuestos_update" ON presupuestos;
DROP POLICY IF EXISTS "presupuestos_delete" ON presupuestos;

DROP POLICY IF EXISTS "recurrencias_select" ON recurrencias;
DROP POLICY IF EXISTS "recurrencias_insert" ON recurrencias;
DROP POLICY IF EXISTS "recurrencias_update" ON recurrencias;
DROP POLICY IF EXISTS "recurrencias_delete" ON recurrencias;

DROP POLICY IF EXISTS "financiamientos_select" ON financiamientos;
DROP POLICY IF EXISTS "financiamientos_insert" ON financiamientos;
DROP POLICY IF EXISTS "financiamientos_update" ON financiamientos;
DROP POLICY IF EXISTS "financiamientos_delete" ON financiamientos;

DROP POLICY IF EXISTS "auth_select_used_tokens" ON used_tokens;
DROP POLICY IF EXISTS "auth_insert_used_tokens" ON used_tokens;

DROP POLICY IF EXISTS "auth_select_hogares" ON hogares;
DROP POLICY IF EXISTS "auth_insert_hogares" ON hogares;
DROP POLICY IF EXISTS "auth_update_hogares" ON hogares;

DROP POLICY IF EXISTS "auth_select_hogar_miembros" ON hogar_miembros;
DROP POLICY IF EXISTS "auth_insert_hogar_miembros" ON hogar_miembros;
DROP POLICY IF EXISTS "auth_delete_hogar_miembros" ON hogar_miembros;

-- ============================================
-- 2. Drop Foreign Keys
-- ============================================
ALTER TABLE gastos DROP CONSTRAINT IF EXISTS fk_gastos_usuario;
ALTER TABLE categorias DROP CONSTRAINT IF EXISTS fk_categorias_usuario;
ALTER TABLE presupuestos DROP CONSTRAINT IF EXISTS fk_presupuestos_usuario;
ALTER TABLE recurrencias DROP CONSTRAINT IF EXISTS fk_recurrencias_usuario;
ALTER TABLE financiamientos DROP CONSTRAINT IF EXISTS fk_financiamientos_usuario;

-- ============================================
-- 3. Drop triggers
-- ============================================
DROP TRIGGER IF EXISTS trg_gastos_updated_at ON gastos;
DROP TRIGGER IF EXISTS trg_categorias_updated_at ON categorias;
DROP TRIGGER IF EXISTS trg_presupuestos_updated_at ON presupuestos;
DROP TRIGGER IF EXISTS trg_recurrencias_updated_at ON recurrencias;
DROP TRIGGER IF EXISTS trg_financiamientos_updated_at ON financiamientos;

-- ============================================
-- 4. Drop trigger function
-- ============================================
DROP FUNCTION IF EXISTS update_updated_at_column;

-- ============================================
-- 5. Drop indexes
-- ============================================
DROP INDEX IF EXISTS idx_gastos_usuario_id;
DROP INDEX IF EXISTS idx_recurrencias_usuario_id;
DROP INDEX IF EXISTS idx_financiamientos_usuario_id;
DROP INDEX IF EXISTS idx_gastos_updated_at;
DROP INDEX IF EXISTS idx_categorias_updated_at;
DROP INDEX IF EXISTS idx_presupuestos_updated_at;
DROP INDEX IF EXISTS idx_recurrencias_updated_at;
DROP INDEX IF EXISTS idx_financiamientos_updated_at;

-- ============================================
-- 6. Drop columns
-- ============================================
ALTER TABLE financiamientos DROP COLUMN IF EXISTS updated_at;
ALTER TABLE recurrencias DROP COLUMN IF EXISTS updated_at;
ALTER TABLE presupuestos DROP COLUMN IF EXISTS updated_at;
ALTER TABLE categorias DROP COLUMN IF EXISTS updated_at;
ALTER TABLE gastos DROP COLUMN IF EXISTS updated_at;

ALTER TABLE financiamientos DROP COLUMN IF EXISTS usuario_id;
ALTER TABLE recurrencias DROP COLUMN IF EXISTS usuario_id;
ALTER TABLE gastos DROP COLUMN IF EXISTS usuario_id;
