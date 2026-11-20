# Auditoría y reportes

Implementación: [`AuditLog`](../../../backend/FlowDesk.Domain/Audit/AuditLog.cs), `AuditWriter` en
[`CoreServices.cs`](../../../backend/FlowDesk.Infrastructure/Services/CoreServices.cs),
[`ReportingServices.cs`](../../../backend/FlowDesk.Application/Reporting/ReportingServices.cs),
trigger en [`008_CreateAuditLogs.sql`](../../../database/Scripts/008_CreateAuditLogs.sql).

## Requisito: registro de auditoría
Cada operación de negocio DEBE agregar un `AuditLog` en la misma unidad de trabajo, con fecha UTC, usuario (o "Sistema"), acción,
entidad, id, estado anterior y nuevo (máx. 200), detalle (máx. 1.000) y correlation id.

| Acción | Cuándo |
|---|---|
| `auth.login` / `auth.loginFailed` | Inicio de sesión correcto o fallido (actor explícito, aún sin token) |
| `expedient.created` / `expedient.assigned` / `expedient.transition` | Alta, asignación, cada transición |
| `document.uploaded` / `document.downloaded` / `document.processed` | Carga, consulta del contenido, resultado del Worker |
| `user.created` / `user.updated` | Administración de usuarios |

## Requisito: inalterable
`AuditLog` NO DEBE exponer métodos de modificación. En SQL Server, `TR_AuditLogs_Immutable` DEBE impedir `UPDATE` y `DELETE`.

### Escenario: intento de manipulación
- **Cuando** se ejecuta `UPDATE dbo.AuditLogs SET Details = N'manipulado'`
- **Entonces** falla con el error 50001 "La auditoría es inalterable…"

## Requisito: correlación
La API DEBE aceptar `X-Correlation-Id` (1–64 caracteres) o generar uno, devolverlo en la respuesta, incluirlo en los errores y en el
scope de logs, y guardarlo en la auditoría y en el Outbox. El Worker DEBE reutilizar el del mensaje en su auditoría y sus logs.

## Requisito: consulta de auditoría
`GET /api/audit?entityId=&action=&page=&pageSize=` (`audit.read`) DEBE devolver los registros del más reciente al más antiguo, paginados.

## Requisito: dashboard
`GET /api/dashboard` (`expedients.read`) DEBE devolver: expedientes abiertos (no finales), asignados al usuario, pendientes de
aprobación, aprobados y rechazados en 30 días, tiempo medio de resolución (horas, cerrados en 90 días), conteo por cada estado y
las 10 actividades de negocio más recientes (excluye `auth.*` y `document.downloaded`).

## Requisito: reportes SQL
`vw_ExpedientSummary` (resumen con responsable, documentos e inválidos, antigüedad) y `usp_ExpedientStatusReport(@From, @To)`;
`usp_PurgeProcessedOutbox(@RetentionDays)` para mantenimiento.
