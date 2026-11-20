# Expedientes

Implementación: agregado [`Expedient`](../../../backend/FlowDesk.Domain/Expedients/Expedient.cs),
[`ExpedientService`](../../../backend/FlowDesk.Application/Expedients/ExpedientService.cs),
endpoints en [`Endpoints.cs`](../../../backend/FlowDesk.API/Endpoints/Endpoints.cs).

## Endpoints

| Método | Ruta | Permiso | Respuesta |
|---|---|---|---|
| GET | `/api/expedients?status=&search=&assignedToMe=&priority=&page=&pageSize=` | `expedients.read` | 200 `PagedResult<ExpedientSummaryDto>` |
| GET | `/api/expedients/{id}` | `expedients.read` | 200 `ExpedientDetailDto` · 404 |
| POST | `/api/expedients` | `expedients.create` | 201 · 400 |
| POST | `/api/expedients/{id}/assign` | `expedients.assign` | 200 · 400 · 409 · 422 |
| POST | `/api/expedients/{id}/transitions` | `expedients.read` + permiso de la transición | ver [workflow](../workflow-aprobaciones/spec.md) |

## Requisito: alta
Un expediente DEBE tener título (1–200), solicitante (1–150), prioridad (`Low`, `Normal`, `High`, `Urgent`) y PUEDE tener
descripción (máx. 2.000) y responsable activo. DEBE nacer en `Received`, con código `EXP-aaaammdd-XXXXXX` único, una entrada de
historial (∅ → `Received`), el evento `ExpedientCreated` y el registro de auditoría `expedient.created`.

### Escenario: responsable inexistente
- **Dado** un `assignedToId` que no existe o está inactivo
- **Cuando** se crea el expediente
- **Entonces** recibe 400 con el campo `assignedToId`

## Requisito: bandeja
La búsqueda DEBE filtrar por uno o varios estados (`status=A,B`), prioridad, texto (código, título o solicitante) y "asignados a mí";
DEBE ordenar por prioridad descendente y luego por `UpdatedAt` descendente, y paginar en el servidor (`pageSize` entre 1 y 100).
El detalle DEBE incluir documentos, historial, aprobaciones, el `concurrencyStamp` y las transiciones disponibles **para el usuario actual**.

### Escenario: estado inválido en el filtro
- **Cuando** consulta `?status=Abierto`
- **Entonces** recibe 400 con el campo `status`

## Requisito: asignación
La asignación DEBE exigir un usuario activo, NO DEBE permitirse en estados finales (422) y DEBE emitir `ExpedientAssigned` (notifica al
nuevo responsable) y auditar `expedient.assigned` con el responsable anterior y el nuevo.

## Requisito: concurrencia optimista
Toda modificación DEBE rotar `ConcurrencyStamp`, que es token de concurrencia en EF. Los comandos DEBEN enviar el stamp que leyeron.

### Escenario: dos usuarios sobre el mismo expediente
- **Dado** que A y B leyeron el expediente con el stamp `s1`
- **Cuando** A ejecuta una transición (el stamp pasa a `s2`) y luego B envía la suya con `s1`
- **Entonces** B recibe 409 "El registro fue modificado por otro usuario. Recargue e intente de nuevo."
