-- ============================================
-- Migración v10: Restaurar acceso anónimo a hogares y hogar_miembros
-- ============================================
-- La app usa la anon key del cliente para operaciones REST.
-- El usuario puede crear un hogar sin haber iniciado sesión en Supabase Auth.
-- Las policies auth_* existentes (TO authenticated) conviven sin problema.
-- Ver también migración v9 que restauró anon para used_tokens.

-- ============================================
-- hogares
-- ============================================
CREATE POLICY "anon_select_hogares" ON hogares
    FOR SELECT TO anon
    USING (true);

CREATE POLICY "anon_insert_hogares" ON hogares
    FOR INSERT TO anon
    WITH CHECK (true);

-- ============================================
-- hogar_miembros
-- ============================================
CREATE POLICY "anon_select_hogar_miembros" ON hogar_miembros
    FOR SELECT TO anon
    USING (true);

CREATE POLICY "anon_insert_hogar_miembros" ON hogar_miembros
    FOR INSERT TO anon
    WITH CHECK (true);

CREATE POLICY "anon_delete_hogar_miembros" ON hogar_miembros
    FOR DELETE TO anon
    USING (true);
