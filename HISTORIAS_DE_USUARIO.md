# Historias de usuario

Cada historia indica la prueba automatizada (o la verificación documentada) que la comprueba.
Estado: ✅ verificada · 🟡 implementada, falta prueba o una parte · ⬜ pendiente.
Las reglas citadas (RE, RD, RW) están en el [PRD](PRD.md) y el comportamiento detallado en [`openspec/`](openspec/project.md).
Lo hecho y lo pendiente, tarea por tarea, está en [`openspec/changes/`](openspec/project.md#tareas) (`[x]` / `[ ]`).

Pruebas: `backend/FlowDesk.Tests` (xUnit) · `e2e/playwright/tests` (Playwright) · `docs/presentacion/video/escritorio.ps1`
(maneja FlowDesk Capture con UI Automation; es la verificación documentada del escritorio).

| Fase | Historias |
|---|---|
| 1 · Fundamentos | HU-01 a HU-06 (HU-04 y HU-05 🟡) |
| 2 · Expedientes y documentos | HU-07 a HU-15 (HU-08, HU-09, HU-10 y HU-14 🟡) |
| 3 · Workflow y auditoría | HU-16 a HU-24 (HU-19 y HU-23 🟡) |
| 4 · Escritorio y TWAIN | HU-25 a HU-30 (HU-26 y HU-30 🟡) |
| 5 · Tiempo real y AWS | HU-31 a HU-34 (HU-33 🟡) |
| 6 · Calidad y despliegue | HU-35 a HU-39 (HU-37 y HU-38 🟡) |

---

## Módulo 1 · Identidad y acceso

**HU-01 · Iniciar sesión** ✅
Como usuario quiero entrar con correo y contraseña para acceder solo a lo que mi rol permite.
- *Dado* credenciales válidas, *cuando* inicio sesión, *entonces* recibo un JWT con mis roles y permisos y entro al dashboard.
- *Dado* credenciales inválidas, *entonces* veo "Credenciales inválidas" y queda un registro `auth.loginFailed` en la auditoría.
- *Dado* que no hay sesión, *cuando* abro una ruta protegida, *entonces* vuelvo al login.
- Verifica: E2E `rechaza credenciales inválidas`, `redirige a login si no hay sesión`, `el operador accede al dashboard`.

**HU-02 · Navegación según el rol** ✅
Como usuario quiero ver solo las opciones de mi rol.
- *Dado* un operador, *entonces* no ve Auditoría ni Usuarios; *dado* un administrador, ve ambas.
- *Cuando* un operador escribe `/audit` en la barra, *entonces* vuelve al inicio.
- Verifica: E2E `el menú se adapta al rol`, `el administrador ve auditoría y usuarios`, `una ruta sin permiso redirige al inicio`.

**HU-03 · Autorización en el servidor** ✅
Como responsable de seguridad quiero que la API decida, no la interfaz.
- *Dado* un operador, *cuando* llama `GET /api/audit`, *entonces* recibe 403.
- *Dado* un usuario sin el permiso de una transición, *cuando* la ejecuta por API, *entonces* recibe 403 desde el dominio.
- Verifica: E2E `el backend valida permisos aunque la UI se salte`; `ExpedientWorkflowTests.User_without_permission_cannot_transition`.

**HU-04 · Administrar usuarios y roles** 🟡
Como administrador quiero crear usuarios, asignarles roles y desactivarlos.
- *Entonces* el correo es único, la contraseña tiene al menos 8 caracteres y los roles deben existir (400 si no).
- *Entonces* cada alta y cambio queda en la auditoría con el estado anterior y el nuevo.
- *Entonces* veo la matriz de roles y permisos.
- Verifica: pantalla Usuarios (E2E `el administrador ve auditoría y usuarios`, video). Falta prueba de alta y edición (tarea 6.3).

**HU-05 · Proteger el inicio de sesión** 🟡
Como responsable de seguridad quiero limitar los intentos de login.
- *Entonces* más de 10 intentos por minuto desde una IP reciben 429 y la web muestra "Demasiados intentos".
- *Entonces* las contraseñas se guardan con PBKDF2-SHA256 y sal aleatoria.
- Verifica: `PasswordHasherTests.Hash_verifies_and_uses_random_salt`; el 429 se observó al ejecutar el E2E con el límite por defecto. Falta prueba automatizada (tarea 6.4).

**HU-06 · Base de datos versionada** ✅
Como DBA quiero un esquema reproducible y revisable.
- *Entonces* `database/deploy.ps1` aplica los scripts numerados una sola vez (`SchemaVersions`) y vistas, procedimientos y seed siempre.
- *Entonces* la API funciona contra SQL Server con `EnsureCreated = false`.
- Verifica: dos ejecuciones de `deploy.ps1` en LocalDB (la segunda omite los scripts) y prueba de humo completa contra ese esquema.

## Módulo 2 · Expedientes y documentos

**HU-07 · Crear un expediente** ✅
Como operador quiero registrar una solicitud con título, solicitante, prioridad y responsable.
- *Entonces* nace en `Received` con código único y una entrada en el historial (RE-01 a RE-03).
- Verifica: E2E `el operador crea el expediente y carga un documento`; `Create_starts_received_with_history_and_event`.

**HU-08 · Bandeja de trabajo** 🟡
Como validador quiero ver los expedientes con filtros y prioridades.
- *Entonces* puedo filtrar por estado, prioridad, texto y "asignados a mí", con paginación en el servidor (máx. 100 por página).
- *Entonces* se ordena por prioridad y luego por última actualización.
- Verifica: E2E abre expedientes desde la bandeja y desde Aprobaciones (filtro por estado). Falta probar cada filtro (tarea 6.5).

**HU-09 · Asignar responsable** 🟡
Como validador quiero asignar el expediente a una persona.
- *Entonces* el responsable debe estar activo; no se reasigna un expediente cerrado (RE-04).
- *Entonces* el nuevo responsable recibe una notificación.
- Verifica: asignación al crear (video, escena 2). Falta prueba de `POST /api/expedients/{id}/assign` (tarea 6.6).

**HU-10 · Edición concurrente segura** 🟡
Como usuario quiero saber si otro modificó el expediente antes que yo.
- *Dado* un `concurrencyStamp` desactualizado, *cuando* ejecuto una transición o una asignación, *entonces* recibo 409.
- Verifica: `Each_change_rotates_concurrency_stamp`; 409 comprobado en la prueba de humo manual. Falta prueba automatizada (tarea 6.7).

**HU-11 · Cargar documentos desde la web** ✅
Como operador quiero adjuntar PDF e imágenes desde el navegador.
- *Entonces* el documento queda en `Pending` hasta que el Worker lo procesa (RD-02, RD-04).
- Verifica: E2E `el operador crea el expediente y carga un documento`.

**HU-12 · Validar el formato real** ✅
Como responsable de calidad quiero rechazar archivos dañados o disfrazados.
- *Dado* un archivo cuya firma binaria no coincide con la extensión, *entonces* se rechaza con "El contenido del archivo no corresponde con su extensión" (RD-01).
- Verifica: `FileSignatureValidatorTests` (5 pruebas); E2E `un archivo con firma falsa es rechazado`.

**HU-13 · Versionar documentos** ✅
Como operador quiero reemplazar un documento sin perder el anterior.
- *Dado* un archivo con el mismo nombre, *entonces* se crea la versión 2 y el documento vuelve a `Pending` (RD-03).
- Verifica: `Same_file_name_creates_new_version_instead_of_duplicate`.

**HU-14 · Visor documental** 🟡
Como validador quiero ver el documento, sus versiones y su hash sin descargarlo.
- *Entonces* PDF en el visor del navegador, imágenes en línea, TIFF como descarga; cada consulta queda auditada (`document.downloaded`).
- Verifica: video (escenas 10 y 13). Falta prueba automatizada del visor y del selector de versión (tarea 6.8).

**HU-15 · Procesamiento asíncrono del documento** ✅
Como operador quiero que la verificación pesada no me haga esperar.
- *Entonces* el Worker calcula el SHA-256, revalida el formato y detecta duplicados en el expediente (RD-05), de forma idempotente (RD-06).
- Verifica: `DocumentProcessingTests` (4 pruebas); E2E espera "Validado" tras la carga.

## Módulo 3 · Workflow y auditoría

**HU-16 · Transiciones controladas** ✅
Como responsable del proceso quiero que el estado solo cambie por transiciones definidas.
- *Dado* una transición que no sale del estado actual, *entonces* recibo 422 `invalid_transition`.
- *Entonces* solo veo los botones de las transiciones que mi rol permite.
- Verifica: `Transition_from_wrong_state_is_rejected`, `Available_transitions_depend_on_permissions`; E2E `el operador no puede validar`.

**HU-17 · Condiciones para avanzar** ✅
Como aprobador quiero recibir solo expedientes completos.
- *Entonces* no se valida sin documentos y no se envía a aprobación con documentos en proceso o inválidos (RW-04).
- Verifica: `Validation_requires_documents`, `Cannot_send_to_approval_while_documents_are_processing`.

**HU-18 · Observación obligatoria** ✅
Como auditor quiero saber por qué se rechazó o devolvió un expediente.
- *Entonces* rechazar, solicitar corrección y devolver exigen observación (RW-03): 422 en la API, botón deshabilitado en la web.
- Verifica: `Reject_requires_comment`; E2E `rechazar exige una observación`.

**HU-19 · Corrección y reenvío** 🟡
Como operador quiero corregir un expediente devuelto y reenviarlo.
- *Dado* `CorrectionRequested`, *entonces* puedo cargar nuevas versiones y reenviar a validación (`resubmit`).
- Verifica: transición definida y cubierta por las reglas generales. Falta prueba del ciclo completo (tarea 6.9).

**HU-20 · Decisión de aprobación** ✅
Como aprobador quiero aprobar, rechazar o devolver y que quede constancia.
- *Entonces* se registra la aprobación con decisión, responsable y observación; los estados finales fijan `ClosedAt` (RW-05).
- Verifica: `Happy_path_reaches_approved_and_records_approval`; E2E `el aprobador aprueba y queda el historial`.

**HU-21 · Historial del expediente** ✅
Como cualquier usuario quiero ver cada cambio de estado con su responsable, fecha y observación.
- Verifica: E2E `el aprobador aprueba y queda el historial` (pestaña Historial).

**HU-22 · Auditoría inalterable** ✅
Como auditor quiero un registro de operaciones que nadie pueda alterar.
- *Entonces* login, creación, asignación, transición, carga, consulta y procesamiento de documentos, y cambios de usuarios quedan auditados en la misma transacción.
- *Cuando* alguien intenta `UPDATE` o `DELETE` sobre `AuditLogs` en SQL Server, *entonces* falla (error 50001).
- Verifica: trigger `TR_AuditLogs_Immutable` probado con `sqlcmd`; pantalla Auditoría (E2E y video). En SQLite (desarrollo) no hay trigger.

**HU-23 · Seguir una operación de punta a punta** 🟡
Como soporte quiero un identificador que una la petición, los logs, el Outbox, el Worker y la auditoría.
- *Entonces* la API acepta o genera `X-Correlation-Id`, lo devuelve, lo incluye en los errores y lo guarda en la auditoría y en el Outbox; el Worker lo reutiliza.
- Verifica: correlation id visible en la auditoría (video). Falta prueba automatizada (tarea 6.10).

**HU-24 · Dashboard operativo** ✅
Como usuario quiero ver indicadores y actividad reciente.
- *Entonces* veo expedientes abiertos, asignados a mí, pendientes de aprobación, aprobados y rechazados en 30 días, tiempo medio de resolución, distribución por estado y actividad de negocio (sin logins ni consultas).
- Verifica: E2E `el operador accede al dashboard`; video.

## Módulo 4 · FlowDesk Capture (escritorio) y TWAIN

**HU-25 · Iniciar sesión y elegir el expediente** ✅
Como operador quiero entrar en la app de escritorio y elegir o crear el expediente destino.
- *Entonces* solo veo expedientes que admiten documentos (RD-02) y puedo crear uno si tengo `expedients.create`.
- Verifica: `escritorio.ps1`, escenas E1 y E2.

**HU-26 · Capturar PDF e imágenes** 🟡
Como operador quiero cargar archivos por varias vías.
- *Entonces* puedo usar los botones (PDF, imágenes, cualquier formato), arrastrar y soltar, "Abrir con" (argumentos) o la carpeta vigilada.
- Verifica: carpeta vigilada en `escritorio.ps1` (E3); "Abrir con" verificado con capturas; los botones abren el cuadro de diálogo con su filtro.
  Falta probar de punta a punta la carga por cuadro de diálogo y por arrastrar y soltar (tarea 4.9).

**HU-27 · Validar antes de enviar** ✅
Como operador quiero enterarme de un archivo inválido sin esperar al servidor.
- *Entonces* la app aplica `FileSignatureValidator` (las mismas reglas que la API) y muestra el motivo del rechazo.
- Verifica: `escritorio.ps1` E3 (`cedula.pdf` rechazada); `FileSignatureValidatorTests`.

**HU-28 · Vista previa** ✅
Como operador quiero revisar cada documento antes de enviarlo.
- *Entonces* PDF en WebView2 e imágenes en línea, con tipo, tamaño, páginas y origen.
- Verifica: `escritorio.ps1` E4.

**HU-29 · Enviar y conocer el resultado** ✅
Como operador quiero enviar y saber si el servidor aceptó cada documento.
- *Entonces* cada documento sube con su progreso y la app consulta su estado hasta "Validado" o "Inválido" con el motivo.
- Verifica: `escritorio.ps1` E6 y E7.

**HU-30 · Escanear con TWAIN** 🟡
Como operador quiero digitalizar desde un escáner físico.
- *Entonces* el escaneo produce un TIFF multipágina con origen `Scanner`, por el mismo endpoint, sin cambiar el negocio.
- *Dado* `Capture:Twain:Driver = None`, *entonces* el botón aparece deshabilitado con el motivo.
- Verifica: simulador en `escritorio.ps1` E5. Falta la sesión nativa con un escáner real (tarea 4.8).

## Módulo 5 · Tiempo real y AWS

**HU-31 · Actualización en tiempo real** ✅
Como usuario quiero ver los cambios de otros sin recargar.
- *Entonces* SignalR actualiza la bandeja, el dashboard y el detalle; la barra lateral muestra el estado de la conexión.
- Verifica: E2E `el operador crea el expediente y carga un documento` (el estado pasa a "Validado" sin recargar).

**HU-32 · Notificaciones** ✅
Como usuario quiero enterarme de lo que me afecta.
- *Entonces* el creador y el responsable reciben una notificación por cada cambio de estado (salvo quien lo hizo); el responsable, al crear o asignar; quien cargó, si su documento es inválido.
- Verifica: prueba de humo (3 notificaciones del operador tras el flujo); video, escena 15.

**HU-33 · Eventos sin pérdida (Outbox + SQS)** 🟡
Como responsable técnico quiero que ningún evento se pierda ni se procese dos veces.
- *Entonces* los eventos se guardan en el Outbox con el cambio; el dispatcher los publica (SQS o en proceso) con reintentos y el consumidor registra `ProcessedMessages`.
- Verifica: `Saving_aggregate_writes_domain_events_to_outbox_in_same_transaction`, `Processing_twice_is_a_no_op`; flujo completo con SQS de LocalStack en docker-compose. Falta comprobar el paso a la DLQ (tarea 5.12).

**HU-34 · Archivos en S3** ✅
Como responsable técnico quiero los binarios fuera de la base, cifrados.
- *Entonces* `S3FileStorage` sube con SSE-AES256; la base guarda la clave (RD-04).
- Verifica: prueba de humo en docker-compose contra S3 de LocalStack (objeto creado y servido al visor). Falta probarlo en una cuenta AWS real.

## Módulo 6 · Calidad y despliegue

**HU-35 · Pruebas automatizadas** ✅
- Verifica: 27 pruebas xUnit y 13 E2E de Playwright en verde.

**HU-36 · Entorno completo con Docker** ✅
- *Entonces* `docker compose up` levanta SQL Server (con los scripts), LocalStack (S3/SQS), API, Worker y web.
- Verifica: `docker compose up --build`; prueba de humo completa contra la API (:5080) y web en :8081 con tiempo real activo.

**HU-37 · Infraestructura AWS como código** 🟡
- *Entonces* `infrastructure/aws/flowdesk.yaml` crea S3, SQS + DLQ, RDS SQL Server, CloudWatch e IAM de mínimo privilegio.
- Verifica: plantilla escrita. Falta validarla (`cfn-lint`) y desplegarla (tarea 6.12).

**HU-38 · Integración continua** 🟡
- *Entonces* GitHub Actions ejecuta backend, escritorio, frontend y E2E.
- Verifica: `.github/workflows/ci.yml`. Falta repositorio git y ejecución (tarea 6.13).

**HU-39 · Video del camino feliz** ✅
- Verifica: `docs/presentacion/videos/01-camino-feliz.mp4` (4 min 35 s, narrado), reproducible con `docs/presentacion/video/grabar.js`.
