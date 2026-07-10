# Control de Gastos

Aplicación PWA de finanzas personales con gamificación RPG, construida con Blazor WebAssembly .NET 8.  
Ofrece registro de gastos, presupuestos, reportes gráficos, tienda de títulos/íconos, niveles de experiencia, PIN de bloqueo y más.

## Arquitectura

- **Frontend**: Blazor WebAssembly (`ControlDeGastos/`)
- **API**: Azure Functions .NET 8 isolated (`ControlDeGastos.Functions/`)
- **Base de datos**: IndexedDB (offline-first) + Supabase (validación de tokens)
- **Hosting**: Azure Static Web Apps (plan gratuito)
- **Licencias**: Token-based con firma ECDSA P-256

## Generar tokens

Los tokens usan firma ECDSA P-256. La clave pública está embebida en `Program.cs` y `TokenValidator.cs`.

### Requisitos

```bash
# Generar par de llaves (solo la primera vez)
cd tools/KeyGenerator
dotnet run
# Crea private.key y public.key en tools/KeyGenerator/
```

### Uso

```bash
cd tools/TokenGenerator

# Trial por 180 días, plan LOCAL, sin gamificación
dotnet run -- --key ..\KeyGenerator\private.key trial

# Trial personalizado: 30 días, plan NUBE, con gamificación
dotnet run -- --key ..\KeyGenerator\private.key trial 30 nube gameon

# Para siempre (vitalicio), plan LOCAL, con gamificación
dotnet run -- --key ..\KeyGenerator\private.key forever local gameon

# Ayuda
dotnet run -- --key ..\KeyGenerator\private.key
```

### Formato del token

```
CDGv2|TIPO|EXPIRY_TICKS|PLAN|GAME|FIRMA
```

| Parte | Valores |
|-------|---------|
| `TIPO` | `TRIAL` o `FOREVER` |
| `PLAN` | `LOCAL` o `NUBE` |
| `GAME` | `GAMEON` o `GAMEOFF` |

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

## Historial de cambios

### v14 — Scroll en tienda + inputs sin 0 + propagar categoría a gastos + hamburguesa más grande
- Al comprar o equipar en la Tienda, el scroll ya no se resetea (se quitó el spinner de recarga)
- Los inputs de monto en Gastos, Presupuestos y Financiamiento ya no muestran "0" por defecto (usan `decimal?`)
- Al cambiar la categoría de una recurrencia o financiamiento, los gastos existentes se actualizan automáticamente (reflejado en gráficos de pastel, reportes, etc.)
- Hamburguesa +50% más grande (antes ~28×27px, ahora ~42×40px)

### v13 — Font-size 16px en inputs + demos onboarding + Son of God al final
- Todos los inputs de texto y número con `font-size: 16px` para evitar zoom automático en iOS Safari
- Eliminadas las tarjetas demo de los 5 pasos del onboarding
- "Son of God" movido al final de los títulos en la tienda
- "Creado por EF Systems" agregado en Configuración

### v12 — Cerrar sidebar al navegar + evitar zoom iOS
- Al seleccionar una página en el menú hamburguesa, el sidebar se cierra automáticamente
- `touch-action: manipulation` en `html, body` elimina el doble-tap zoom en iOS Safari

### v11 — Recovery code para PIN
- Al configurar PIN por primera vez, se genera un código de recuperación (`CDG-XXXX-XXXX`)
- Si olvida el PIN: desde la pantalla de bloqueo o desde Configuración → Eliminar PIN, puede usar el recovery code para restablecer o eliminar el PIN
- El recovery code es permanente (no cambia al reconfigurar el PIN)
- Acepta múltiples formatos: `CDG-A7K2-M9P4`, `A7K2-M9P4`, `a7k2m9p4`

### v10 — Botón "Forzar actualización"
- Eliminada la detección automática de actualizaciones del Service Worker
- Reemplazado por un botón manual en Configuración con confirmación
- Limpia la caché del SW y recarga la app sin tocar datos del usuario

### v9 — Fix serialización + fallback LOCAL
- Corregido error de serialización (`token` → `Token`) que causaba "Token vacío" del servidor
- El fallback LOCAL ya no depende de que la API devuelva `Valido=true`
