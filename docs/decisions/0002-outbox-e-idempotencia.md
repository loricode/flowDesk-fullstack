# ADR 0002 · Patrón Outbox y consumidor idempotente

**Estado:** aceptada

## Contexto
Guardar un cambio en SQL Server y publicar un mensaje en SQS son dos sistemas distintos. Sin
coordinación aparecen dos fallos: cambios confirmados cuyo evento nunca se publica, o eventos de
operaciones que terminaron con rollback.

## Decisión
- Los agregados acumulan eventos de dominio. `FlowDeskDbContext.SaveChangesAsync` los serializa en
  `OutboxMessages` **en la misma transacción** que el cambio de negocio.
- `OutboxDispatcherService` (Worker) publica los pendientes en SQS y marca `ProcessedAt`; los fallos
  incrementan `Attempts` y guardan `LastError`.
- El consumidor registra cada `MessageId` en `ProcessedMessages` y omite los repetidos. Además, las
  operaciones son idempotentes por estado (`Document.CompleteProcessing` ignora versiones obsoletas).

## Consecuencias
- Entrega "al menos una vez" con efectos "exactamente una vez" desde el punto de vista del negocio.
- Latencia adicional de hasta un ciclo de sondeo (2 s por defecto).
- En desarrollo, `Messaging:Provider = InProcess` reemplaza SQS sin cambiar el código.
