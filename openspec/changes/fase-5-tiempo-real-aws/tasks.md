# Tareas · Fase 5 · Tiempo real y AWS

## 1. Mensajería
- [x] 5.1 HU-33 · Eventos de dominio → `OutboxMessages` en la misma transacción
- [x] 5.2 HU-33 · `OutboxDispatcherService` con lotes, reintentos (`Attempts`, `LastError`) e `IMessageBus` (`InProcess` / `Sqs`)
- [x] 5.3 HU-33 · `SqsConsumerService` (long polling) e `IntegrationEventDispatcher` idempotente (`ProcessedMessages`)
- [x] 5.4 Worker independiente; modo `Worker:RunInApi` para demos y E2E

## 2. Tiempo real y notificaciones
- [x] 5.5 HU-31 · `NotificationsHub` (grupos por usuario y expediente), `SignalRRealtimeNotifier`, relé `POST /internal/realtime` con clave
- [x] 5.6 HU-32 · `BusinessEventReactions`: notificaciones persistidas, campana en la web, invalidación de consultas

## 3. AWS
- [x] 5.9 HU-34 · `S3FileStorage` con SSE y clientes S3/SQS configurables (región o `ServiceURL` de LocalStack)
- [x] 5.7 HU-33 · Flujo completo con `Messaging:Provider = Sqs` contra LocalStack en docker-compose (cola consumida, 4 eventos procesados)
- [ ] 5.12 HU-33 · Comprobar el paso a la DLQ tras 5 fallos de procesamiento
- [x] 5.8 HU-34 · Carga y descarga de documentos contra S3 de LocalStack (objeto en `expedients/{id}/…`, visor web)
- [ ] 5.10 Bloqueo `UPDLOCK, READPAST` en la lectura del Outbox para varios Workers sobre SQL Server
- [ ] 5.11 Backplane de SignalR (Redis) para varias instancias de la API
