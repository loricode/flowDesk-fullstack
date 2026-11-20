# FlowDesk.Database

Scripts SQL Server versionados e idempotentes. Son la fuente de verdad del esquema en entornos
compartidos (ver [ADR 0006](../docs/decisions/0006-esquema-por-scripts-versionados.md)).

```
Scripts/           000-009  Tablas, claves, índices, CHECK, trigger de auditoría (se aplican una vez)
Views/             vw_ExpedientSummary
StoredProcedures/  usp_ExpedientStatusReport, usp_PurgeProcessedOutbox
Seed/              Permisos, roles, matriz rol-permiso y flujo estándar
deploy.ps1         Aplica todo en orden y registra cada script en dbo.SchemaVersions
```

## Aplicar

```powershell
# LocalDB
./deploy.ps1 -Server "(localdb)\MSSQLLocalDB" -Database FlowDesk

# Amazon RDS
./deploy.ps1 -Server <endpoint> -Database FlowDesk -User flowdesk_admin -Password $env:DB_PASSWORD
```

Después, configurar la API con:

```json
"Database": { "Provider": "SqlServer", "EnsureCreated": false, "SeedDemoData": true },
"ConnectionStrings": { "FlowDesk": "Server=...;Database=FlowDesk;..." }
```

Los usuarios de demostración los crea la API al iniciar (`SeedDemoData`), porque el hash de
contraseña (PBKDF2) se calcula en .NET.

## Modelo

| Tabla | Propósito |
| --- | --- |
| Users, Roles, Permissions, UserRoles, RolePermissions | Identidad y autorización por permisos |
| Expedients | Expedientes (estado, prioridad, responsable, `ConcurrencyStamp`) |
| ExpedientHistory | Historial de estados con responsable y observación |
| Documents, DocumentVersions | Metadata y versiones; el archivo vive en S3 (`StorageKey`) |
| WorkflowDefinitions, WorkflowTransitions | Transiciones permitidas, permiso requerido y si exigen observación |
| Approvals | Decisiones de aprobación, rechazo o devolución |
| Notifications | Notificaciones por usuario |
| AuditLogs | Auditoría inalterable (trigger `TR_AuditLogs_Immutable`) |
| OutboxMessages, ProcessedMessages | Patrón Outbox e idempotencia del consumidor |
| SchemaVersions | Control de scripts aplicados |

## Agregar un cambio

1. Crear `Scripts/0NN_Descripcion.sql` idempotente (`IF COL_LENGTH(...) IS NULL ...`).
2. Actualizar la configuración de EF en `FlowDesk.Infrastructure/Persistence/Configurations.cs`.
3. Ejecutar `deploy.ps1` y las pruebas.
