# Fase 1 · Fundamentos

## Por qué
Antes de modelar el negocio hace falta una base que no haya que rehacer: arquitectura por capas y módulos, persistencia
versionada, identidad con permisos validados en el servidor y la estructura del frontend.

## Qué cambia
- Solución .NET 10 con Clean Architecture (Domain, Application, Infrastructure, API, Worker, Realtime, Shared, Tests).
- EF Core con SQL Server y SQLite; scripts SQL versionados y `deploy.ps1`.
- JWT, roles y permisos, administración de usuarios, rate limiting.
- Monorepo del frontend (npm workspaces): app web y paquetes `ui-components`, `api-client`, `auth`, `shared-types`.

## Specs afectadas
[identidad-acceso](../../../specs/identidad-acceso/spec.md), [soporte-produccion](../../../specs/soporte-produccion/spec.md)

## Historias
HU-01 a HU-06.
