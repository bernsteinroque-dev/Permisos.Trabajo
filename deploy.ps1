# PTS Synthon — Script de Deploy
# Ejecutar desde C:\Permisos.Trabajo como Administrador
# Uso: .\deploy.ps1

$ErrorActionPreference = "Stop"
$proyecto = "src\PTS_Synthon\PTS_Synthon.csproj"
$destino  = "C:\inetpub\wwwroot\PTS_Synthon"

Write-Host "`n=== PTS Synthon Deploy ===" -ForegroundColor Cyan

# 1. Obtener ultima version sin abrir editor
Write-Host "`n[1/4] Actualizando codigo..." -ForegroundColor Yellow
git pull --rebase origin claude/vibrant-faraday-y3cnlt
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR en git pull" -ForegroundColor Red; exit 1 }

# 2. Detener IIS
Write-Host "`n[2/4] Deteniendo IIS..." -ForegroundColor Yellow
iisreset /stop | Out-Null
Write-Host "IIS detenido." -ForegroundColor Green

# 3. Compilar y publicar
Write-Host "`n[3/4] Compilando y publicando..." -ForegroundColor Yellow
dotnet publish $proyecto -c Release -o $destino /p:TreatWarningsAsErrors=false
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR en dotnet publish. Reiniciando IIS..." -ForegroundColor Red
    iisreset /start | Out-Null
    exit 1
}

# 4. Iniciar IIS
Write-Host "`n[4/4] Iniciando IIS..." -ForegroundColor Yellow
iisreset /start | Out-Null
Write-Host "IIS iniciado." -ForegroundColor Green

Write-Host "`n=== Deploy completado exitosamente ===" -ForegroundColor Cyan
