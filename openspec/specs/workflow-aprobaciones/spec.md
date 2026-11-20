# Workflow y aprobaciones

Implementación: [`WorkflowDefinition`](../../../backend/FlowDesk.Domain/Workflow/WorkflowDefinition.cs),
`Expedient.ApplyTransition` en [`Expedient.cs`](../../../backend/FlowDesk.Domain/Expedients/Expedient.cs),
seed en [`database/Seed/002_SeedWorkflow.sql`](../../../database/Seed/002_SeedWorkflow.sql).

## Estados

```
Received ──► InValidation ──► PendingApproval ──► Approved
                │   ▲                │
                │   └─ CorrectionRequested ◄─┤
                └──► Rejected ◄──────────────┘
```

`Approved` y `Rejected` son finales: fijan `ClosedAt` y no admiten documentos, transiciones ni reasignación.

## Requisito: transiciones definidas
Solo existen las transiciones del flujo activo `EXPEDIENT_DEFAULT` (tabla `WorkflowTransitions`):

| Código | Nombre | Desde | Hacia | Permiso | Observación |
|---|---|---|---|---|---|
| `start-validation` | Iniciar validación | Received | InValidation | workflow.validate | opcional |
| `send-to-approval` | Enviar a aprobación | InValidation | PendingApproval | workflow.validate | opcional |
| `request-correction` | Solicitar corrección | InValidation | CorrectionRequested | workflow.validate | **obligatoria** |
| `reject-validation` | Rechazar en validación | InValidation | Rejected | workflow.validate | **obligatoria** |
| `approve` | Aprobar | PendingApproval | Approved | workflow.approve | opcional |
| `reject` | Rechazar | PendingApproval | Rejected | workflow.approve | **obligatoria** |
| `return-correction` | Devolver para corrección | PendingApproval | CorrectionRequested | workflow.approve | **obligatoria** |
| `resubmit` | Reenviar a validación | CorrectionRequested | InValidation | documents.upload | opcional |

`POST /api/expedients/{id}/transitions` con `{ transitionCode, comment, concurrencyStamp }`.

## Requisito: validaciones de la transición
En este orden, `ApplyTransition` DEBE rechazar:
1. si el estado actual no es el de origen → 422 `invalid_transition`;
2. si el usuario no tiene el permiso → 403 `forbidden`;
3. si exige observación y viene vacía → 422 `comment_required`;
4. si no se cumplen las condiciones de negocio → 422:
   - hacia `InValidation` sin documentos → `documents_required`;
   - hacia `PendingApproval` sin documentos → `documents_required`, con alguno `Pending` → `documents_processing`, con alguno `Invalid` → `documents_invalid`.

### Escenario: aprobar desde Recibido
- **Dado** un expediente en `Received`
- **Cuando** el aprobador ejecuta `approve`
- **Entonces** recibe 422 `invalid_transition` y el estado no cambia

### Escenario: el operador intenta validar
- **Dado** un expediente en `Received` con documentos
- **Cuando** el operador ejecuta `start-validation` por API
- **Entonces** recibe 403 y en la web no ve el botón

## Requisito: efectos de una transición válida
DEBE: cambiar el estado; agregar una entrada al historial (desde, hacia, responsable, observación, fecha UTC); si sale de
`PendingApproval`, registrar una aprobación (`Approved`, `Rejected` o `CorrectionRequested`); fijar `ClosedAt` si el destino es final;
rotar el stamp; emitir `ExpedientStatusChanged`; auditar `expedient.transition` con estados anterior y nuevo y la observación.

### Escenario: camino feliz
- **Dado** un expediente con documentos validados
- **Cuando** la validadora ejecuta `start-validation` y `send-to-approval`, y el aprobador `approve` con observación
- **Entonces** queda `Approved`, con 4 entradas de historial, una aprobación y `ClosedAt`

## Requisito: transiciones disponibles
El detalle DEBE listar solo las transiciones que salen del estado actual y cuyo permiso tiene el usuario, ordenadas por `SortOrder`.
