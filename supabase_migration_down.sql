-- Migration DOWN: Revert Hogar Compartido + Used Tokens
-- Run in Supabase SQL Editor
-- WARNING: This will DELETE DATA. Use with caution.

-- ============================================
-- 4. Remove hogar_id from existing tables
-- ============================================
ALTER TABLE presupuestos DROP COLUMN IF EXISTS hogar_id;
ALTER TABLE categorias DROP COLUMN IF EXISTS hogar_id;
ALTER TABLE gastos DROP COLUMN IF EXISTS hogar_id;

-- ============================================
-- 3. Drop hogar_miembros
-- ============================================
DROP POLICY IF EXISTS "anon_delete_hogar_miembros" ON hogar_miembros;
DROP POLICY IF EXISTS "anon_insert_hogar_miembros" ON hogar_miembros;
DROP POLICY IF EXISTS "anon_select_hogar_miembros" ON hogar_miembros;

DROP TABLE IF EXISTS hogar_miembros;

-- ============================================
-- 2. Drop hogares
-- ============================================
DROP POLICY IF EXISTS "anon_insert_hogares" ON hogares;
DROP POLICY IF EXISTS "anon_select_hogares" ON hogares;

DROP TABLE IF EXISTS hogares;

-- ============================================
-- 1. Drop used_tokens
-- ============================================
DROP POLICY IF EXISTS "anon_select_used_tokens" ON used_tokens;
DROP POLICY IF EXISTS "anon_insert_used_tokens" ON used_tokens;

DROP TABLE IF EXISTS used_tokens;
