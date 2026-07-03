# Control de Gastos

Aplicación PWA de finanzas personales construida con Blazor WebAssembly .NET 8.

## Arquitectura

- **Frontend**: Blazor WebAssembly (`ControlDeGastos/`)
- **API**: Azure Functions .NET 8 isolated (`ControlDeGastos.Functions/`)
- **Base de datos**: IndexedDB (offline-first) + Supabase (licencias)
- **Hosting**: Azure Static Web Apps

## Publicar en Azure Static Web Apps

### 1. Migración SQL en Supabase

Ejecuta esta SQL en el SQL Editor de tu proyecto Supabase:

```sql
CREATE TABLE IF NOT EXISTS revoked_tokens (
    token_hash TEXT PRIMARY KEY,
    revoked_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    reason TEXT NOT NULL DEFAULT ''
);

ALTER TABLE revoked_tokens ENABLE ROW LEVEL SECURITY;

CREATE POLICY "service_role_all_revoked_tokens" ON revoked_tokens
    FOR ALL TO service_role
    USING (true)
    WITH CHECK (true);
```

### 2. Crear Azure Static Web App

Desde [portal.azure.com](https://portal.azure.com):

- **Recurso**: Static Web App
- **Plan**: Gratuito (Free)
- **Origen**: GitHub
- **Repositorio**: `PabloFlore/ControlDeGastos`
- **Branch**: `main`
- **Build Preset**: Blazor
- **App location**: `ControlDeGastos`
- **Api location**: `ControlDeGastos.Functions`
- **Output location**: `wwwroot`

### 3. Variables de entorno en Azure

Después de crear el recurso, ve a **Settings → Environment variables** y agrega:

| Nombre | Valor |
|--------|-------|
| `Supabase__Url` | `https://upwbdekcbqjntundleet.supabase.co` |
| `Supabase__ServiceRoleKey` | (tu Service Role Key de Supabase) |
| `Revocation__ApiKey` | (clave secreta para revocar tokens) |

La `ServiceRoleKey` se obtiene de Supabase → **Settings → API → service_role secret**.

### 4. Workflow de GitHub Actions

El archivo `.github/workflows/azure-static-web-apps.yml` ya está configurado. Publica tanto el frontend como las Functions automáticamente en cada push a `main`.

### 5. Prueba local

```bash
cd ControlDeGastos.Functions
func start
```

Endpoint           | Método | Ruta
-------------------|--------|----------------------------
Health             | GET    | `/api/health`
Validate           | POST   | `/api/license/validate`
Activate           | POST   | `/api/license/activate`
Revoke             | POST   | `/api/license/revoke`
Revoked List       | GET    | `/api/license/revoked`

## Notas

- La app usa IndexedDB como almacenamiento local (offline-first). No requiere base de datos externa.
- Supabase se usa para validación de licencias y revocación de tokens.
- El Service Worker se registra automáticamente para soporte offline.
- Las Functions se ejecutan en el plan de consumo (costo $0 cuando no se usan).
