# Tiempo real, mensajería y notificaciones

Implementación: Outbox en [`FlowDeskDbContext`](../../../backend/FlowDesk.Infrastructure/Persistence/FlowDeskDbContext.cs),
[`Messaging/`](../../../backend/FlowDesk.Infrastructure/Messaging), [`BusinessEventReactions`](../../../backend/FlowDesk.Application/Notifications/NotificationServices.cs),
[`NotificationsHub`](../../../backend/FlowDesk.Realtime/NotificationsHub.cs), [`FlowDesk.Worker`](../../../backend/FlowDesk.Worker/Program.cs).

## Requisito: Outbox transaccional
Al guardar, el `DbContext` DEBE convertir los eventos de dominio de los agregados (`ExpedientCreated`, `ExpedientAssigned`,
`ExpedientStatusChanged`, `DocumentUploaded`) en `OutboxMessages` (Id = EventId, tipo, JSON, fecha, correlation id) **en la misma
transacción** que el cambio.

## Requisito: publicación
`OutboxDispatcherService` DEBE leer cada `Messaging:PollingIntervalSeconds` (2) hasta `BatchSize` (20) mensajes sin procesar con
`Attempts < MaxAttempts` (5), en orden de ocurrencia, y publicarlos con `IMessageBus`:
- `InProcess`: entrega directa al despachador (desarrollo, pruebas, `Worker:RunInApi`);
- `Sqs`: `SendMessage` con atributo `type` (y `MessageGroupId`/`MessageDeduplicationId` si la cola es FIFO); `SqsConsumerService`
  consume con long polling (20 s, 10 mensajes) y borra solo lo procesado. Tras 5 recepciones fallidas, SQS lo mueve a la DLQ.
Un fallo DEBE incrementar `Attempts` y guardar `LastError`.

## Requisito: consumo idempotente
`IntegrationEventDispatcher` DEBE omitir los mensajes ya registrados en `ProcessedMessages` (`MessageId`, consumidor) y registrar
cada mensaje procesado. Las reacciones DEBEN ser idempotentes por estado.

### Escenario: entrega repetida
- **Dado** un `DocumentUploaded` ya procesado
- **Cuando** SQS lo entrega otra vez
- **Entonces** no se recalcula nada ni se crean notificaciones nuevas

## Requisito: notificaciones
| Evento | Destinatarios (excepto quien actuó) | Mensaje |
|---|---|---|
| `ExpedientCreated` con responsable | responsable | "Se le asignó el expediente {código}." |
| `ExpedientAssigned` | nuevo responsable | "Se le asignó el expediente {código}." |
| `ExpedientStatusChanged` | creador y responsable | "El expediente {código} pasó de «X» a «Y»." |
| Documento `Invalid` tras procesar | quien lo cargó | "'{archivo}' inválido: {motivo}" |

Se persisten en `Notifications`. `GET /api/notifications?unreadOnly=` devuelve las 50 más recientes del usuario;
`POST /api/notifications/read-all` las marca leídas.

## Requisito: tiempo real
El hub `/hubs/notifications` (autenticado) DEBE agregar cada conexión al grupo `user:{id}` y permitir `JoinExpedient` /
`LeaveExpedient`. Cada reacción DEBE publicar un `RealtimeEvent` (`expedient.created`, `expedient.statusChanged`,
`expedient.assigned`, `document.uploaded`, `document.processed`, `notification`): los de tipo `notification` al grupo del usuario,
el resto a todos. La web DEBE invalidar sus consultas (`expedients`, `dashboard`, `expedient/{id}`, `notifications`) al recibirlos.

El Worker NO aloja el hub: DEBE entregar los eventos a `POST /internal/realtime` con `X-Internal-Key`; una clave ausente o distinta
recibe 401. La notificación en tiempo real es "best effort": si falla, el estado ya está persistido.

### Escenario: documento procesado
- **Dado** la validadora con el expediente abierto en la web
- **Cuando** el operador envía un documento desde el escritorio y el Worker lo valida
- **Entonces** el documento pasa a "Validado" en la web sin recargar
