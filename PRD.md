# PRD · FlowDesk

Plataforma de gestión documental y automatización de procesos de aprobación (Enterprise Document & Workflow Management).

## Problema

Una empresa recibe a diario cientos de documentos de clientes, proveedores y empleados para abrir y resolver solicitudes
(expedientes). Hoy:

- Los documentos llegan por canales distintos (correo, papel, carpetas compartidas) y se escanean a mano.
- Nadie sabe con certeza en qué etapa está cada expediente ni quién lo tiene.
- Las aprobaciones se hacen por correo: no hay reglas, cualquiera "cambia el estado" y no queda constancia de la decisión.
- Se suben documentos duplicados, archivos dañados o con la extensión cambiada.
- Ante una auditoría no se puede reconstruir quién hizo qué, cuándo y sobre qué documento.

## Objetivo del MVP

Centralizar la captura documental (escritorio y web), controlar el ciclo de vida del expediente con un **workflow de reglas
explícitas** y dejar **trazabilidad completa**, con una arquitectura modular preparada para integrar escáneres TWAIN y servicios AWS.

| Métrica | Meta del MVP | Resultado medido | Cómo se mide |
|---|---|---|---|
| Cambios de estado fuera del workflow | 0 | 0: la API no expone "cambiar estado"; toda transición pasa por `Expedient.ApplyTransition` | `ExpedientWorkflowTests.Transition_from_wrong_state_is_rejected` |
| Transiciones ejecutadas sin permiso | 0, validado en el servidor | 403 desde el dominio aunque la UI se salte | `User_without_permission_cannot_transition`, E2E `el backend valida permisos aunque la UI se salte` |
| Archivos disfrazados aceptados | 0: se valida la firma binaria, no solo la extensión | Rechazados en escritorio (antes de enviar), API (400) y Worker | `FileSignatureValidatorTests`, E2E `un archivo con firma falsa es rechazado` |
| Documentos duplicados sin detectar en un expediente | 0 | Mismo SHA-256 → documento `Invalid` con el nombre del original | `Identical_content_in_same_expedient_is_flagged_as_duplicate` |
| Eventos de negocio perdidos | 0 | Outbox en la misma transacción que el cambio | `Saving_aggregate_writes_domain_events_to_outbox_in_same_transaction` |
| Efectos repetidos ante mensajes duplicados | 0 | Segundo procesamiento = sin cambios | `Processing_twice_is_a_no_op`, tabla `ProcessedMessages` |
| Ediciones simultáneas que se pisan | 0 | La segunda recibe 409 | `Each_change_rotates_concurrency_stamp` + prueba de humo manual (409) |
| Tiempo desde la carga hasta "Validado" | < 10 s en desarrollo | ≤ 5 s (sondeo del Outbox cada 2 s) | Prueba de humo y E2E `el operador crea el expediente y carga un documento` |
| Registros de auditoría modificables | 0 | `UPDATE`/`DELETE` fallan con error 50001 | Trigger `TR_AuditLogs_Immutable`, verificado con `sqlcmd` en SQL Server |

## Usuarios y roles

| Rol | Qué necesita | Permisos |
|---|---|---|
| **Operador** | Crear expedientes y digitalizar documentos | `expedients.read`, `expedients.create`, `documents.upload` |
| **Validador** | Revisar documentos, asignar y enviar a aprobación o pedir correcciones | `expedients.read`, `expedients.assign`, `workflow.validate`, `documents.upload` |
| **Aprobador** | Aprobar, rechazar o devolver | `expedients.read`, `workflow.approve` |
| **Auditor** | Reconstruir quién hizo qué y cuándo | `expedients.read`, `audit.read` |
| **Administrador** | Gestionar usuarios y roles | Todos |

Los usuarios del MVP son de demostración (`operador`, `validador`, `aprobador`, `auditor`, `admin` `@flowdesk.local`) y los crea el seed.

## Alcance

### MVP 1.0 (entregado)

1. Identidad: JWT, roles y permisos, administración de usuarios, rate limiting en el login.
2. Expedientes: creación, bandeja con filtros y paginación, asignación, concurrencia optimista.
3. Gestión documental: carga web y escritorio, validación de firma, versionado por nombre, almacenamiento S3/local, visor.
4. Workflow configurable (transiciones en base de datos) con permisos, observaciones obligatorias y condiciones de negocio.
5. Procesamiento asíncrono: Outbox → (SQS o en proceso) → Worker: hash SHA-256, revalidación, duplicados, notificaciones.
6. Tiempo real con SignalR y notificaciones persistidas por usuario.
7. Auditoría de solo inserción con `X-Correlation-Id`; dashboard operativo.
8. FlowDesk Capture (WPF): captura por archivos, arrastrar, "Abrir con" y carpeta vigilada; vista previa; captura TWAIN desacoplada con simulador.
9. Scripts SQL Server versionados, docker-compose, CloudFormation, pruebas xUnit y Playwright, CI, video narrado.

### Versión 2

- Sesión TWAIN nativa (`NativeTwainSession`) con un escáner real.
- OIDC con el proveedor corporativo y refresh tokens (hoy JWT de 8 h; los permisos se renuevan al volver a iniciar sesión).
- Backplane de SignalR (Redis) para varias instancias de la API.
- Editor de workflows y varios flujos por tipo de expediente.
- OCR y extracción de datos de los documentos; búsqueda de texto completo.
- Despliegue en ECS/Fargate y alarmas operativas sobre la DLQ.

### Fuera de alcance

Firma electrónica, gestión de retención legal, integración con ERP, multiempresa (tenants) y aplicación móvil.

## Reglas de negocio

### Expedientes

| Id | Regla |
|---|---|
| RE-01 | Título (máx. 200) y solicitante (máx. 150) obligatorios; descripción opcional (máx. 2.000). |
| RE-02 | Prioridad: `Low`, `Normal`, `High`, `Urgent`. Código único `EXP-aaaammdd-XXXXXX`. |
| RE-03 | Un expediente nace en `Received` y registra la primera entrada del historial. |
| RE-04 | El responsable debe existir y estar activo. No se reasigna un expediente cerrado. |
| RE-05 | Toda modificación rota el `ConcurrencyStamp`; una petición con un stamp desactualizado recibe 409. |

### Documentos

| Id | Regla |
|---|---|
| RD-01 | Formatos: PDF, PNG, JPG/JPEG, TIFF. Máximo 20 MB. El contenido (firma binaria) debe coincidir con la extensión. |
| RD-02 | Solo se agregan documentos en `Received`, `InValidation` o `CorrectionRequested`. |
| RD-03 | Un archivo con el mismo nombre (sin distinguir mayúsculas) en el mismo expediente crea una **nueva versión** y vuelve a `Pending`. |
| RD-04 | El archivo se guarda en S3 (o disco en desarrollo); la base guarda clave, tamaño, tipo, origen, versión y SHA-256. Si la metadata no se guarda, el archivo se elimina. |
| RD-05 | El Worker calcula el SHA-256, revalida el formato y marca `Invalid` si otro documento del mismo expediente tiene el mismo hash. |
| RD-06 | El procesamiento es idempotente: ignora versiones obsoletas y documentos ya procesados. |
| RD-07 | Origen del documento: `Web`, `DesktopFile` o `Scanner`. |

### Workflow

| Id | Regla |
|---|---|
| RW-01 | Estados: `Received`, `InValidation`, `PendingApproval`, `CorrectionRequested`, `Approved`, `Rejected`. `Approved` y `Rejected` son finales y fijan `ClosedAt`. |
| RW-02 | Solo existen las transiciones de `WorkflowTransitions` (tabla en [openspec](openspec/specs/workflow-aprobaciones/spec.md)); cada una exige un permiso. |
| RW-03 | Rechazar, solicitar corrección y devolver exigen observación. |
| RW-04 | Pasar a `InValidation` exige al menos un documento. Pasar a `PendingApproval` exige documentos, ninguno `Pending` y ninguno `Invalid`. |
| RW-05 | Salir de `PendingApproval` registra una aprobación (`Approved`, `Rejected` o `CorrectionRequested`) con responsable y observación. |
| RW-06 | Cada transición agrega una entrada al historial y emite `ExpedientStatusChanged`. |

## Requisitos no funcionales

- **Seguridad**: JWT en cada petición, una política por permiso en cada endpoint y permiso revalidado en el dominio. PBKDF2-SHA256
  (100.000 iteraciones). Login limitado a 10 intentos por minuto e IP. Los archivos se sirven solo con token, sin URLs públicas.
- **Consistencia**: el cambio de negocio, la auditoría y el Outbox se guardan en una sola transacción.
- **Resiliencia**: reintentos del Outbox (5), DLQ en SQS, notificación en tiempo real "best effort" (el estado ya está persistido).
- **Trazabilidad**: auditoría inalterable con usuario, UTC, estado anterior y nuevo, y correlation id de cliente → API → Outbox → Worker.
- **Escalabilidad**: Worker independiente y horizontal; archivos en S3; consumidor idempotente.
- **Mantenibilidad**: Clean Architecture por módulos, contratos compartidos (`FlowDesk.Shared`) entre WPF, API y Worker.
- **Usabilidad**: interfaz en español con Material Design (MUI y MaterialDesignInXaml), paleta sapote, gris y negro azulado.

## Tecnologías y responsabilidad

| Tecnología | Responsabilidad | Fase |
|---|---|---|
| .NET 10 · ASP.NET Core · EF Core 10 | API REST, casos de uso, persistencia | 1–3 ✅ |
| SQL Server (RDS) · SQLite en desarrollo | Datos, invariantes, auditoría inalterable | 1 ✅ |
| WPF · MaterialDesignInXaml · WebView2 | FlowDesk Capture | 4 ✅ (TWAIN nativo pendiente) |
| TWAIN | Captura desde escáner detrás de `ITwainSession` | 4 🟡 (simulador) |
| React 19 · MUI 7 · React Query | Aplicación web | 1–3 ✅ |
| SignalR | Tiempo real | 5 ✅ |
| AWS S3 · SQS · RDS · CloudWatch · IAM | Infraestructura | 5 🟡 (código listo, sin desplegar) |
| xUnit · Playwright | Pruebas | 6 ✅ |
| Docker · GitHub Actions | Entornos y CI | 6 🟡 (definidos, sin ejecutar) |

## Riesgos y decisiones

| Riesgo | Mitigación |
|---|---|
| Acoplar el negocio a un driver de escáner | `IDocumentCaptureService` + `ITwainSession` ([ADR 0004](docs/decisions/0004-captura-desacoplada-twain.md)). |
| Perder eventos entre la base y la cola | Outbox transaccional + consumidor idempotente ([ADR 0002](docs/decisions/0002-outbox-e-idempotencia.md)). |
| Esquema y modelo desalineados | Scripts generados desde el modelo de EF y API probada contra SQL Server sin `EnsureCreated` ([ADR 0006](docs/decisions/0006-esquema-por-scripts-versionados.md)). |
| Sobrediseño (microservicios) para un MVP | Monolito modular + Worker ([ADR 0001](docs/decisions/0001-monolito-modular.md)). |
| Seguridad delegada en la UI | Políticas por permiso en la API y validación en el dominio. |
