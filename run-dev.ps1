<#
.SYNOPSIS
    Levanta FlowDesk en modo desarrollo: API, Worker, web y (opcional) la app de escritorio.
    Cada proceso se abre en su propia ventana para ver sus logs.

.EXAMPLE
    ./run-dev.ps1            # API + Worker + Web
    ./run-dev.ps1 -Desktop   # además abre FlowDesk Capture (WPF)
#>
param([switch] $Desktop)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

Write-Host "Compilando solución .NET..." -ForegroundColor Cyan
dotnet build (Join-Path $root 'FlowDesk.slnx') -v q
if ($LASTEXITCODE -ne 0) { throw 'La compilación falló.' }

if (-not (Test-Path (Join-Path $root 'frontend/node_modules'))) {
    Write-Host "Instalando dependencias del frontend..." -ForegroundColor Cyan
    Push-Location (Join-Path $root 'frontend'); npm install; Pop-Location
}

function Start-Window([string] $Title, [string] $Folder, [string] $Command) {
    Start-Process powershell -ArgumentList '-NoExit', '-Command', "`$Host.UI.RawUI.WindowTitle = '$Title'; Set-Location '$Folder'; $Command"
}

Start-Window 'FlowDesk.API'    "$root/backend/FlowDesk.API"    'dotnet run --no-build --launch-profile http'
Start-Window 'FlowDesk.Worker' "$root/backend/FlowDesk.Worker" 'dotnet run --no-build'
Start-Window 'FlowDesk.Web'    "$root/frontend"                'npm run dev'

if ($Desktop) {
    Start-Process (Join-Path $root 'desktop/FlowDesk.Desktop/bin/Debug/net10.0-windows/FlowDesk.Capture.exe')
}

Write-Host ""
Write-Host "API ......... http://localhost:5080  (OpenAPI: /openapi/v1.json)" -ForegroundColor Green
Write-Host "Web ......... http://localhost:5173" -ForegroundColor Green
Write-Host "Usuarios .... operador / validador / aprobador / auditor / admin  @flowdesk.local" -ForegroundColor Green
