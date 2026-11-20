# Arquitectura de FlowDesk

## Vista de componentes

```mermaid
flowchart LR
    subgraph Clientes
        WPF["FlowDesk Capture<br/>WPF + Material Design<br/>(PDF, imágenes, TWAIN)"]
        WEB["FlowDesk Web<br/>React + MUI"]
    end

    subgraph Backend [".NET 10 · monolito modular"]
        API["FlowDesk.API<br/>REST + JWT + ProblemDetails"]
        HUB["FlowDesk.Realtime<br/>SignalR Hub"]
        APP["FlowDesk.Application<br/>casos de uso"]
        DOM["FlowDesk.Domain<br/>agregados, workflow, eventos"]
        INF["FlowDesk.Infrastructure<br/>EF Core, S3, SQS, Outbox"]
        WRK["FlowDesk.Worker<br/>procesamiento asíncrono"]
    end

    subgraph AWS
        RDS[("RDS SQL Server")]
        S3[("S3 documentos")]
        SQS[["SQS + DLQ"]]
        CW["CloudWatch"]
    end

    WPF -- REST --> API
    WEB -- REST --> API
    WEB <-- WebSockets --> HUB
    API --> APP --> DOM
    APP --> INF
    INF --> RDS
    INF --> S3
    WRK -- publica Outbox --> SQS
    SQS -- consume --> WRK
    WRK --> INF
    WRK -- relé interno --> HUB
    API -. logs JSON .-> CW
    WRK -. logs JSON .-> CW
```

### Dependencias entre capas

```
API ──► Application ──► Domain
 │           ▲
 └──► Infrastructure ──┘        Worker ──► Infrastructure
Realtime ──► Application         Desktop ──► Shared (contratos)
```

- **Domain** no depende de nada: reglas puras y testeables.
- **Application** define puertos (`IAppDbContext`, `IFileStorage`, `IRealtimeNotifier`…) y orquesta los casos de uso.
- **Infrastructure** implementa los puertos. Cambiar de S3 a disco o de SQS a proceso es configuración, no código.

## Módulos de negocio

| Módulo | Responsabilidad | Código |
| --- | --- | --- |
| Identity | Usuarios, roles, permisos, JWT | `Domain/Identity`, `Application/Identity` |
| Expedients | Expedientes, asignación | `Domain/Expedients`, `Application/Expedients` |
| DocumentManagement | Documentos, versiones, procesamiento | `Domain/Documents`, `Application/Documents` |
| Workflow | Definiciones y transiciones | `Domain/Workflow` |
| Notifications | Notificaciones persistidas y en tiempo real | `Application/Notifications`, `Realtime` |
| Audit | Registro inalterable | `Domain/Audit`, `Infrastructure/Services/AuditWriter` |
| Reporting | Dashboard y consultas de auditoría | `Application/Reporting` |

## Carga de un documento (Outbox + procesamiento asíncrono)

```mermaid
sequenceDiagram
    autonumber
    participant C as WPF / Web
    participant A as API
    participant S3 as S3
    participant DB as SQL Server
    participant W as Worker
    participant Q as SQS
    participant H as SignalR

    C->>A: POST /api/expedients/{id}/documents
    A->>A: Validar firma binaria, tamaño y estado del expediente
    A->>S3: PutObject (cifrado SSE)
    A->>DB: BEGIN · Document + DocumentVersion + AuditLog + OutboxMessage · COMMIT
    A-->>C: 201 Created (estado Pending)
    loop cada 2 s
        W->>DB: OutboxMessages pendientes
        W->>Q: SendMessage (MessageId = EventId)
        W->>DB: ProcessedAt = now
    end
    Q->>W: ReceiveMessage
    W->>DB: ¿MessageId ya procesado? (idempotencia)
    W->>S3: GetObject
    W->>W: SHA-256 + revalidar formato + detectar duplicados
    W->>DB: Document = Validated / Invalid · AuditLog · ProcessedMessage
    W->>H: POST /internal/realtime
    H-->>C: ReceiveEvent(document.processed)
```

Si el guardado en base falla después de subir a S3, la API elimina el objeto (compensación) para no
dejar archivos huérfanos. Si falla la publicación a SQS, el mensaje queda en el Outbox y se
reintenta. Si falla el procesamiento, SQS lo reentrega y, tras 5 intentos, lo mueve a la DLQ con una
alarma de CloudWatch.

## Motor de workflow

Las transiciones están en `WorkflowTransitions` (configurables sin recompilar). Cada transición
define estado de origen y destino, permiso requerido y si exige observación. El agregado
`Expedient.ApplyTransition` valida:

1. que el estado actual coincida con el origen;
2. que el usuario tenga el permiso de la transición (`DomainForbiddenException` → 403);
3. la observación obligatoria (→ 422);
4. las condiciones de negocio: documentos presentes, sin documentos en proceso ni inválidos para aprobar (→ 422).

Luego registra el historial, la aprobación (si venía de "Pendiente de aprobación"), la fecha de
cierre y el evento `ExpedientStatusChanged`. La API nunca expone un "cambiar estado" libre.

## Seguridad

- JWT con claims `perm` por permiso; una política de autorización por permiso en cada endpoint.
- El dominio vuelve a validar el permiso de cada transición: ocultar botones en React es solo UX.
- Contraseñas con PBKDF2-SHA256 (100 000 iteraciones, sal aleatoria, comparación en tiempo constante).
- Rate limiting en `/api/auth/login`, CORS restringido y archivos servidos solo con token (sin URLs públicas de S3).
- Validación de firma binaria ("magic number"), no solo de la extensión, compartida entre WPF, API y Worker.

## Escalabilidad

| Necesidad | Solución |
| --- | --- |
| Procesamiento pesado | Workers independientes que escalan horizontalmente según la edad del mensaje más antiguo en SQS |
| Archivos | S3 con versionado y transición a Glacier IR |
| Eventos duplicados | `ProcessedMessages` + deduplicación por `MessageId` (SQS FIFO opcional) |
| Pérdida de eventos | Outbox transaccional + DLQ |
| Conflictos de edición | `ConcurrencyStamp` como token de concurrencia (409 al cliente) |
| Tiempo real con varias instancias de la API | Backplane de SignalR (Redis o Azure SignalR) sin cambiar `IRealtimeNotifier` |
| Trazabilidad | Correlation id de extremo a extremo (cliente → API → Outbox → Worker → auditoría) |
