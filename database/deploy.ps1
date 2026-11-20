<#
.SYNOPSIS
    Aplica los scripts versionados de FlowDesk en SQL Server (local, contenedor o Amazon RDS).

.DESCRIPTION
    Ejecuta en orden Scripts/, Views/, StoredProcedures/ y Seed/. Los scripts de Scripts/ se registran
    en dbo.SchemaVersions y no se vuelven a ejecutar; vistas, procedimientos y seed son idempotentes
    (CREATE OR ALTER / MERGE) y se aplican siempre. Requiere sqlcmd.

.EXAMPLE
    ./deploy.ps1 -Server "(localdb)\MSSQLLocalDB" -Database FlowDesk
    ./deploy.ps1 -Server flowdesk.xxxx.us-east-1.rds.amazonaws.com -Database FlowDesk -User admin -Password $env:DB_PASSWORD
#>
param(
    [Parameter(Mandatory)] [string] $Server,
    [string] $Database = 'FlowDesk',
    [string] $User,
    [string] $Password,
    [switch] $SkipSeed
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

function Get-BaseArgs([string] $Db) {
    $list = @('-S', $Server, '-d', $Db, '-b', '-I', '-f', '65001')
    if ($User) { $list += @('-U', $User, '-P', $Password) } else { $list += '-E' }
    return $list
}

function Invoke-SqlFile([string] $File) {
    $sqlArgs = (Get-BaseArgs $Database) + @('-i', $File)
    & sqlcmd @sqlArgs | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Falló la ejecución de $File" }
}

function Invoke-SqlQuery([string] $Query, [string] $Db = $Database) {
    $sqlArgs = (Get-BaseArgs $Db) + @('-h', '-1', '-W', '-Q', "SET NOCOUNT ON; $Query")
    $output = & sqlcmd @sqlArgs
    if ($LASTEXITCODE -ne 0) { throw "Falló la consulta: $Query" }
    return $output
}

Write-Host "==> Servidor $Server - base $Database" -ForegroundColor Cyan
Invoke-SqlQuery -Db master -Query "IF DB_ID(N'$Database') IS NULL CREATE DATABASE [$Database];" | Out-Null

# 1) Scripts versionados: cada uno se aplica una sola vez.
foreach ($script in Get-ChildItem (Join-Path $root 'Scripts') -Filter *.sql | Sort-Object Name) {
    $name = $script.Name
    $count = Invoke-SqlQuery "IF OBJECT_ID(N'dbo.SchemaVersions') IS NOT NULL SELECT COUNT(*) FROM dbo.SchemaVersions WHERE ScriptName = N'$name' ELSE SELECT 0;"

    if ([int](($count | Where-Object { $_ -match '^\d+$' }) | Select-Object -Last 1) -gt 0) {
        Write-Host "    = $name (ya aplicado)" -ForegroundColor DarkGray
        continue
    }

    Write-Host "    + $name" -ForegroundColor Green
    Invoke-SqlFile $script.FullName
    $hash = (Get-FileHash $script.FullName -Algorithm SHA256).Hash
    Invoke-SqlQuery "INSERT INTO dbo.SchemaVersions (ScriptName, Checksum) VALUES (N'$name', '$hash');" | Out-Null
}

# 2) Objetos programables y datos iniciales (idempotentes).
$folders = @('Views', 'StoredProcedures')
if (-not $SkipSeed) { $folders += 'Seed' }
foreach ($folder in $folders) {
    foreach ($script in Get-ChildItem (Join-Path $root $folder) -Filter *.sql | Sort-Object Name) {
        Write-Host "    ~ $folder/$($script.Name)" -ForegroundColor Yellow
        Invoke-SqlFile $script.FullName
    }
}

Write-Host "==> Base de datos lista." -ForegroundColor Cyan
