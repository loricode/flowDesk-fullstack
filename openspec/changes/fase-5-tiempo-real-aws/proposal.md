# Fase 5 · Tiempo real y AWS

## Por qué
El procesamiento pesado no debe bloquear al usuario, ningún evento debe perderse y los usuarios deben enterarse de los cambios sin
recargar. La infraestructura objetivo es AWS.

## Qué cambia
- Outbox transaccional, despachador con reintentos, bus en proceso o SQS, consumidor idempotente, Worker independiente.
- SignalR (WebSockets) con grupos por usuario y por expediente; relé interno Worker → API.
- Notificaciones persistidas por usuario.
- S3 para archivos, SQS + DLQ, RDS, CloudWatch e IAM.

## Specs afectadas
[tiempo-real-notificaciones](../../specs/tiempo-real-notificaciones/spec.md), [gestion-documental](../../specs/gestion-documental/spec.md)

## Historias
HU-31 a HU-34.
