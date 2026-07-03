-- ============================================
-- Migración v11: Políticas anon para tablas de datos
-- ============================================
-- La app usa la anon key del cliente para operaciones REST.
-- Estas tablas ya tienen políticas auth_* (FOR authenticated),
-- pero también necesitan permisos anon para flujos donde
-- el usuario no ha iniciado sesión en Supabase Auth.
-- ============================================

-- ============================================
-- gastos
-- ============================================
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

-- ============================================
-- categorias
-- ============================================
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

-- ============================================
-- presupuestos
-- ============================================
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

-- ============================================
-- recurrencias
-- ============================================
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

-- ============================================
-- financiamientos
-- ============================================
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

-- ============================================
-- suscripciones
-- ============================================
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

-- ============================================
-- perfiles
-- ============================================
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
