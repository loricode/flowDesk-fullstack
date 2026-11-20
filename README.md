# FlowDesk

**Enterprise Document & Workflow Management Platform**

Plataforma para centralizar la captura documental, automatizar la validación y aprobación de
expedientes y ofrecer trazabilidad completa de cada operación. Integra en una misma solución una
aplicación web (**React + MUI**), una aplicación de escritorio (**WPF + Material Design**) preparada
para escáneres **TWAIN**, un backend **.NET 10** con Clean Architecture y servicios **AWS**.

```
 FlowDesk Capture (WPF)          FlowDesk Web (React + MUI)
   PDF / imágenes / TWAIN          bandejas · visor · aprobaciones
            │  REST                        │  REST + SignalR (WebSockets)
            └──────────────┬───────────────┘
                     FlowDesk.API  ── Application ── Domain
                           │              │
                SQL Server / RDS      Outbox ──► SQS ──► FlowDesk.Worker
                AWS S3 (archivos)                         │ hash, duplicados,
                                                          │ notificaciones
                                     SignalR Hub ◄────────┘
```

## Qué demuestra

| Área | Implementación |
| --- | --- |
| Modelado de dominio | Agregado `Expedient`, motor de workflow con transiciones persistidas, reglas (documentos obligatorios, observación requerida, permisos por transición) |
| Arquitectura | Monolito modular + Clean Architecture; Worker independiente y escalable |
| Consistencia | Patrón **Outbox** en la misma transacción, consumidor **idempotente**, **concurrencia optimista** |
| Seguridad | JWT, autorización por **permisos** validada en backend y dominio, rate limiting en login, PBKDF2 |
| Auditoría | Registro de solo inserción con usuario, UTC, estado anterior/nuevo y **correlation id**; trigger que impide UPDATE/DELETE |
| Integración de dispositivos | `IDocumentCaptureService` → `FileCaptureService` (activo) y `TwainCaptureService` (listo; simulador incluido) |
| Tiempo real | SignalR: bandejas, dashboard y detalle se actualizan sin recargar |
| AWS | S3, SQS + DLQ, RDS SQL Server, CloudWatch, IAM de mínimo privilegio (CloudFormation) |
| Calidad | 27 pruebas unitarias/integración (xUnit) + 13 pruebas E2E (Playwright), CI en GitHub Actions |

## Estructura del monorepo

```
backend/
  FlowDesk.Domain/          Entidades, workflow, eventos y reglas de negocio
  FlowDesk.Application/     Casos de uso (servicios), puertos, DTOs
  FlowDesk.Infrastructure/  EF Core (SQL Server/SQLite), S3, SQS, Outbox, JWT, seed
  FlowDesk.API/             Endpoints REST por módulo, auth, ProblemDetails, SignalR
  FlowDesk.Realtime/        Hub SignalR y notificador
  FlowDesk.Worker/          Outbox dispatcher, consumidor SQS, procesamiento de documentos
  FlowDesk.Shared/          Contratos compartidos con WPF + validador de firmas de archivo
  FlowDesk.Tests/           xUnit: dominio, validación, integración con SQLite
desktop/FlowDesk.Desktop/   FlowDesk Capture (WPF, MaterialDesignInXaml, MVVM)
frontend/
  apps/web/                 React 19 + Vite + MUI 7 + React Query + SignalR
  packages/ui-components/   Tema FlowDesk y componentes reutilizables
  packages/api-client/      Cliente HTTP tipado
  packages/auth/            Sesión y permisos
  packages/shared-types/    Contratos TypeScript
database/                   Scripts SQL versionados, vistas, procedimientos, seed, deploy.ps1
e2e/playwright/             Pruebas E2E del flujo completo
infrastructure/aws/         CloudFormation (S3, SQS, RDS, CloudWatch, IAM)
infrastructure/deployment/  docker-compose con SQL Server + LocalStack
docs/                       Arquitectura y decisiones (ADR)
```

## Puesta en marcha (desarrollo)

Requisitos: .NET SDK 10, Node 24, Windows para la app de escritorio.

```powershell
./run-dev.ps1 -Desktop
```

O por partes:

```powershell
dotnet run --project backend/FlowDesk.API --launch-profile http   # http://localhost:5080
dotnet run --project backend/FlowDesk.Worker
cd frontend; npm install; npm run dev                               # http://localhost:5173
dotnet run --project desktop/FlowDesk.Desktop                       # FlowDesk Capture
```

En desarrollo se usa **SQLite** y **almacenamiento local** (`backend/.data/`), sin dependencias
externas. Para el entorno completo con SQL Server, S3 y SQS emulados:

```powershell
cd infrastructure/deployment; docker compose up --build             # web en http://localhost:8081
```

### Usuarios de demostración

| Usuario | Contraseña | Rol |
| --- | --- | --- |
| operador@flowdesk.local | Operador123! | Operador: crea expedientes y digitaliza |
| validador@flowdesk.local | Validador123! | Validador: revisa y envía a aprobación |
| aprobador@flowdesk.local | Aprobador123! | Aprobador: aprueba, rechaza o devuelve |
| auditor@flowdesk.local | Auditor123! | Auditor: consulta el historial |
| admin@flowdesk.local | Admin123! | Administrador |

## Flujo de negocio

```
Recibido ──► En validación ──► Pendiente de aprobación ──► Aprobado
                 │    ▲                 │
                 │    └── Corrección ◄──┤ (devolver, con observación)
                 └──► Rechazado ◄───────┘ (rechazar, con observación)
```

1. El **operador** crea el expediente y digitaliza documentos desde **FlowDesk Capture** (o los carga en la web).
2. La API valida la firma del archivo, lo guarda en S3 y registra la metadata. El evento va al **Outbox**.
3. El **Worker** calcula el SHA-256, revalida el formato y detecta duplicados. **SignalR** avisa a todos.
4. La **validadora** inicia la validación y envía a aprobación (solo si todos los documentos son válidos).
5. El **aprobador** decide. Cada paso queda en el historial, las aprobaciones y la auditoría.

## App de escritorio y TWAIN

FlowDesk Capture carga **PDF e imágenes** (diálogo, arrastrar y soltar, "Abrir con" o una **carpeta vigilada**
configurable en `Capture:HotFolder:Path`, donde un escáner de red puede depositar archivos), valida formato
y tamaño con las mismas reglas del backend, muestra vista previa (WebView2 para PDF), envía con
progreso y consulta el resultado del procesamiento.

La captura por escáner está desacoplada detrás de `IDocumentCaptureService`. Hoy el botón
**Escanear** aparece deshabilitado (`Capture:Twain:Driver = None`). Para probar el flujo de escaneo
sin hardware, usar `Simulated`. Para conectar un escáner real, ver
[desktop/FlowDesk.Desktop/Capture/Twain/README.md](desktop/FlowDesk.Desktop/Capture/Twain/README.md).

## Pruebas

```powershell
dotnet test backend/FlowDesk.Tests          # 27 pruebas: dominio, validación, Outbox, Worker
cd e2e/playwright; npm install; npx playwright install chromium; npx playwright test   # 13 E2E
```

Playwright levanta la API (con el Worker integrado) y la web automáticamente si no están corriendo.

## Video

[docs/presentacion/videos/01-camino-feliz.mp4](docs/presentacion/videos/01-camino-feliz.mp4) muestra el camino feliz completo
(escritorio + web) en 4½ minutos, narrado. Se regenera con `node docs/presentacion/video/grabar.js`
(ver [LEEME](docs/presentacion/video/LEEME.md)).

## Documentación

- [PRD](PRD.md): problema, métricas, roles, alcance y reglas de negocio
- [Historias de usuario](HISTORIAS_DE_USUARIO.md): 39 historias con su estado y la prueba que las verifica
- [OpenSpec](openspec/project.md): specs por módulo y tareas por fase (`node openspec/progress.mjs`)
- [Arquitectura](docs/architecture/README.md)
- [Decisiones de arquitectura (ADR)](docs/decisions/)
- [Base de datos](database/README.md)
- [Integración TWAIN](desktop/FlowDesk.Desktop/Capture/Twain/README.md)
