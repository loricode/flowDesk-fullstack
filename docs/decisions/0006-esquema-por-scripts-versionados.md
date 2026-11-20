# ADR 0006 · Esquema gestionado con scripts SQL versionados

**Estado:** aceptada

## Decisión
- En entornos compartidos (staging, producción, docker-compose) el esquema lo crean los scripts de
  `database/Scripts` (idempotentes, registrados en `SchemaVersions`), aplicados por `deploy.ps1` o `db-init.sh`.
- Los scripts añaden lo que el modelo de EF no expresa: restricciones CHECK de estados, índices
  filtrados, el trigger de auditoría inalterable, vistas y procedimientos de mantenimiento.
- En desarrollo local con SQLite, `Database:EnsureCreated = true` crea el esquema desde el modelo.

## Verificación
El DDL base se generó desde el modelo de EF (`GenerateCreateScript`) para garantizar que nombres,
tipos e índices coinciden, y la API se probó contra SQL Server con `EnsureCreated = false`.

## Consecuencias
- Los DBA pueden revisar y versionar cada cambio.
- Cada cambio del modelo requiere un nuevo script numerado (`010_...sql`).
