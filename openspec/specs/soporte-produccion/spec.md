# Soporte de producción

Implementación: [`ExceptionHandling.cs`](../../../backend/FlowDesk.API/Infrastructure/ExceptionHandling.cs),
[`database/`](../../../database/README.md), [`infrastructure/`](../../../infrastructure), [`.github/workflows/ci.yml`](../../../.github/workflows/ci.yml).

## Requisito: errores
Todo error DEBE ser `application/problem+json` con `status`, `title`, `detail`, `code` estable y `correlationId`.

| Excepción | Estado | `code` |
|---|---|---|
| `ValidationException` | 400 (con `errors` por campo) | `validation` |
| `UnauthorizedException` | 401 | `unauthorized` |
| `ForbiddenException`, `DomainForbiddenException` | 403 | `forbidden` |
| `NotFoundException` | 404 | `not_found` |
| `ConcurrencyException` | 409 | `concurrency` |
| `DomainException` | 422 | el de la regla (`invalid_transition`, `comment_required`, `documents_required`, …) |
| Cualquier otra | 500 ("Ocurrió un error inesperado.", sin trazas) | `internal` |

## Requisito: salud y logs
`GET /health` DEBE responder 200 sin autenticación. Fuera de desarrollo, API y Worker DEBEN escribir logs JSON (CloudWatch) con
el correlation id en el scope.

## Requisito: base de datos versionada
`database/Scripts/0NN_*.sql` DEBEN ser idempotentes y registrarse en `SchemaVersions`; nombres, tipos e índices DEBEN coincidir
con el modelo de EF. Fuera de desarrollo la API DEBE usar `Database:EnsureCreated = false`.

## Requisito: configuración por entorno
| Clave | Desarrollo | Producción |
|---|---|---|
| `Database:Provider` | `Sqlite` | `SqlServer` (RDS) |
| `Storage:Provider` | `Local` | `S3` |
| `Messaging:Provider` | `InProcess` | `Sqs` |
| `Jwt:SigningKey` | clave de desarrollo | secreto (≥ 32 caracteres; la API no arranca si falta) |
| `RealtimeRelay:InternalApiKey` | clave de desarrollo | secreto |

## Requisito: entornos reproducibles
`infrastructure/deployment/docker-compose.yml` DEBE levantar SQL Server (esquema por `db-init.sh`), LocalStack (S3, SQS + DLQ),
API, Worker y web. Las imágenes .NET DEBEN ejecutarse con el usuario no root `app`.

## Requisito: infraestructura AWS
`infrastructure/aws/flowdesk.yaml` DEBE crear: bucket S3 cifrado, versionado y sin acceso público (solo TLS); cola SQS con DLQ
(5 recepciones); RDS SQL Server cifrado con contraseña en Secrets Manager; log groups; alarmas de DLQ y antigüedad de la cola;
roles IAM de mínimo privilegio para la API y el Worker.

## Requisito: integración continua
El pipeline DEBE ejecutar las pruebas del backend, publicar FlowDesk Capture (Windows), typecheck y build del frontend y las pruebas
E2E de Playwright.
