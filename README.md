# Control de Gastos

Aplicación PWA de finanzas personales construida con Blazor WebAssembly .NET 8.

## Publicar en Azure Static Web Apps

### 1. Crear `staticwebapp.config.json`

El archivo ya está en `wwwroot/staticwebapp.config.json`. Redirige todas las rutas SPA a `index.html` y agrega headers de seguridad.

### 2. Subir a GitHub

```bash
git init
git add .
git commit -m "Primer commit"
gh repo create <nombre> --public --push
```

### 3. Crear Azure Static Web App

Desde [portal.azure.com](https://portal.azure.com):

- **Recurso**: Static Web App
- **Plan**: Gratuito (Free)
- **Origen**: GitHub
- **Repo**: el que creaste en el paso 2
- **Branch**: `main`
- **Build Preset**: Blazor
- **App location**: `ControlDeGastos`
- **Output location**: `wwwroot`

Azure genera automáticamente un GitHub Actions workflow. La primera vez puede fallar porque el preset "Blazor" espera cierta estructura. Si falla, reemplaza el workflow generado con el de abajo.

### 4. Workflow de GitHub Actions (`.github/workflows/azure-static-web-apps.yml`)

Copia este workflow. Azure crea uno automáticamente pero puedes reemplazarlo con este:

```yaml
name: Azure Static Web Apps CI/CD

on:
  push:
    branches: [main]
  pull_request:
    types: [opened, synchronize, reopened, closed]
    branches: [main]

jobs:
  build_and_deploy:
    if: github.event_name == 'push' || (github.event_name == 'pull_request' && github.event.action != 'closed')
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - name: Publish
        run: dotnet publish ControlDeGastos/ControlDeGastos.csproj -c Release -o publish
      - name: Build And Deploy
        id: builddeploy
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: upload
          app_location: publish/wwwroot
          output_location: ""

  close_pull_request:
    if: github.event_name == 'pull_request' && github.event.action == 'closed'
    runs-on: ubuntu-latest
    steps:
      - name: Close PR
        id: closepullrequest
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          action: close
```

> **Importante**: el workflow generado por Azure ya incluye el `AZURE_STATIC_WEB_APPS_API_TOKEN` en los secrets. No lo modifiques ni elimines.

### 5. Build manual (opcional)

```bash
dotnet publish ControlDeGastos/ControlDeGastos.csproj -c Release -o publish
```

El resultado publicable está en `publish/wwwroot/`. Puedes subir esa carpeta manualmente si no usas CI/CD.

## Notas

- La app usa IndexedDB como almacenamiento local (offline-first). No requiere base de datos externa.
- Supabase se usa únicamente para validación de licencias (evitar reuso de tokens). No es necesario para el funcionamiento básico.
- El Service Worker se registra automáticamente para soporte offline.
