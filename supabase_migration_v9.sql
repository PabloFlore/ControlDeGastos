-- ============================================
-- Migración v9: Restaurar acceso anónimo a used_tokens
-- ============================================
-- La validación de licencia Plan Nube ocurre ANTES de autenticar al usuario en Supabase.
-- El usuario ingresa el token → se valida HMAC → se verifica/marca en used_tokens
-- En este momento el usuario NO está autenticado en Supabase (aún no hizo login).
-- used_tokens solo almacena hashes SHA-256 de tokens (un solo uso, sin datos personales).
-- Seguro para acceso anónimo.

-- Restaurar políticas anónimas para used_tokens
CREATE POLICY "anon_select_used_tokens" ON used_tokens
    FOR SELECT TO anon USING (true);

CREATE POLICY "anon_insert_used_tokens" ON used_tokens
    FOR INSERT TO anon WITH CHECK (true);