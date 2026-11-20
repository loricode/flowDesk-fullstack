# Tareas · Fase 3 · Workflow y auditoría

## 1. Workflow
- [x] 3.1 HU-16 · `WorkflowDefinition` y `WorkflowTransition` persistidos; flujo `EXPEDIENT_DEFAULT` en el seed y en SQL
- [x] 3.2 HU-16 · `ApplyTransition`: estado de origen (422), permiso (403), observación (422)
- [x] 3.3 HU-17 · Condiciones: documentos requeridos, ninguno en proceso ni inválido para aprobación
- [x] 3.4 HU-18 · Observación obligatoria en rechazar, solicitar corrección y devolver (API y web)
- [x] 3.5 HU-19 · Transiciones de corrección (`request-correction`, `return-correction`) y reenvío (`resubmit`)
- [x] 3.6 HU-20 · Registro de aprobaciones al salir de `PendingApproval`; `ClosedAt` en estados finales
- [x] 3.7 HU-21 · Historial con responsable, fecha y observación; pestañas Historial y Aprobaciones en la web
- [x] 3.8 Transiciones disponibles según los permisos del usuario; botones y diálogo de confirmación en la web

## 2. Auditoría y reportes
- [x] 3.9 HU-22 · `AuditWriter` en la misma unidad de trabajo; actor explícito en el login
- [x] 3.10 HU-22 · Trigger `TR_AuditLogs_Immutable` (verificado: `UPDATE` falla con 50001)
- [x] 3.11 HU-23 · `X-Correlation-Id` en respuesta, errores, logs, auditoría y Outbox
- [x] 3.12 HU-24 · Dashboard (KPI, distribución por estado, actividad de negocio) y pantalla Auditoría
- [x] 3.13 `vw_ExpedientSummary`, `usp_ExpedientStatusReport`, `usp_PurgeProcessedOutbox`

## 3. Pruebas
- [x] 3.14 `ExpedientWorkflowTests`: camino feliz, estado inválido, permisos, condiciones, observación, idempotencia, transiciones disponibles
- [x] 3.15 E2E `workflow.spec.ts`: crear, cargar, rechazar firma falsa, validar, observación obligatoria, aprobar e historial
