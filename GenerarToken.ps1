<#
.DEPRECATED - Usa tools/TokenGenerator en su lugar.

Este script genera tokens CDGv1 (HMAC) que ya no son válidos.
El nuevo formato es CDGv2 con firma ECDSA.

Usa: dotnet run --project tools/TokenGenerator -- --key tools/KeyGenerator/private.key trial 180 nube gameon
     dotnet run --project tools/TokenGenerator -- --key tools/KeyGenerator/private.key forever local gameoff
#>

Write-Host "`n⚠️  Este script está obsoleto." -ForegroundColor Yellow
Write-Host "Genera tokens CDGv1 (HMAC) que ya no son aceptados por el sistema." -ForegroundColor Yellow
Write-Host "Usa el generador oficial en su lugar:" -ForegroundColor Yellow
Write-Host "  dotnet run --project tools/TokenGenerator -- --key tools/KeyGenerator/private.key <trial|forever> [dias] [local|nube] [gameon|gameoff]" -ForegroundColor Cyan
Write-Host "`nEjemplo:" -ForegroundColor Yellow
Write-Host "  dotnet run --project tools/TokenGenerator -- --key tools/KeyGenerator/private.key trial 180 nube gameon" -ForegroundColor Cyan
return
