# OpenSpec · FlowDesk

Las specs describen **comportamiento observable** con escenarios Dado/Cuando/Entonces. Son el contrato entre el negocio,
el código y los asistentes de IA: antes de cambiar un comportamiento se actualiza su spec, en el mismo commit.

```
openspec/
├── specs/<módulo>/spec.md            qué hace el sistema HOY (requisitos y escenarios)
├── changes/<cambio>/proposal.md      por qué y qué cambia
├── changes/<cambio>/tasks.md         tareas con casillas: [x] hecha · [ ] pendiente
├── changes/archive/<cambio>/         cambios con todas las tareas en [x]
└── progress.mjs                      avance por cambio y lista de pendientes
```

| Spec | Qué cubre | Estado |
|---|---|---|
| [identidad-acceso](specs/identidad-acceso/spec.md) | Login JWT, roles y permisos, matriz por endpoint, usuarios, rate limiting | Implementada (faltan pruebas de usuarios y del 429) |
| [expedientes](specs/expedientes/spec.md) | Alta, bandeja, asignación, concurrencia optimista | Implementada (faltan pruebas de filtros, asignación y 409) |
| [gestion-documental](specs/gestion-documental/spec.md) | Carga, validación de firma, versionado, almacenamiento, procesamiento, visor | Implementada (S3 probado en LocalStack) |
| [workflow-aprobaciones](specs/workflow-aprobaciones/spec.md) | Estados, transiciones, permisos, condiciones, aprobaciones, historial | Implementada (falta prueba del ciclo de corrección) |
| [captura-escritorio](specs/captura-escritorio/spec.md) | FlowDesk Capture: orígenes de captura, validación previa, vista previa, envío, TWAIN | Implementada (falta TWAIN nativo) |
| [tiempo-real-notificaciones](specs/tiempo-real-notificaciones/spec.md) | Outbox, mensajería, SignalR, notificaciones | Implementada (falta probar la DLQ) |
| [auditoria-reportes](specs/auditoria-reportes/spec.md) | Auditoría inalterable, correlation id, dashboard, consultas | Implementada |
| [soporte-produccion](specs/soporte-produccion/spec.md) | Errores, salud, base versionada, Docker, AWS, CI, logs | Implementada (Docker verificado; AWS y CI sin ejecutar) |

## Tareas

| Cambio | Historias | Estado |
|---|---|---|
| [fase-1-fundamentos](changes/archive/fase-1-fundamentos/tasks.md) | HU-01 a HU-06 | Archivado |
| [fase-2-expedientes-documentos](changes/archive/fase-2-expedientes-documentos/tasks.md) | HU-07 a HU-15 | Archivado |
| [fase-3-workflow-auditoria](changes/archive/fase-3-workflow-auditoria/tasks.md) | HU-16 a HU-24 | Archivado |
| [fase-4-escritorio-twain](changes/fase-4-escritorio-twain/tasks.md) | HU-25 a HU-30 | Activo: falta TWAIN nativo y pruebas del escritorio |
| [fase-5-tiempo-real-aws](changes/fase-5-tiempo-real-aws/tasks.md) | HU-31 a HU-34 | Activo: falta la DLQ, varios Workers y backplane |
| [fase-6-calidad-despliegue](changes/fase-6-calidad-despliegue/tasks.md) | HU-35 a HU-39 | Activo: pruebas pendientes, AWS y CI |

Las pruebas que faltan para cerrar historias 🟡 de las fases 1 a 3 están en la fase 6, porque la implementación de esas fases
está terminada y verificada por otras pruebas.

```sh
node openspec/progress.mjs           # avance por cambio y tareas pendientes
node openspec/progress.mjs --check   # falla si un cambio archivado tiene tareas sin marcar
```

**Cómo se trabaja con las tareas**

1. Un trabajo nuevo se agrega como `- [ ] N.M descripción` en el `tasks.md` del cambio activo, o en un cambio nuevo
   (`changes/<nombre>/proposal.md` + `tasks.md`) si es un tema aparte.
2. Al terminarlo se marca `- [x]` **en el mismo commit** que el código, con su prueba. Si cambia el comportamiento, también se
   actualiza la spec de `specs/`. Una tarea solo se marca cuando está verificada (prueba automatizada o verificación documentada).
3. Si al trabajar aparece algo que falta, se agrega como `[ ]` en vez de dejarlo en un comentario.
4. Cuando todas las tareas de un cambio están en `[x]`, la carpeta se mueve a `changes/archive/` y se actualiza esta tabla.

## Convenciones

- **Stack**: .NET 10 (ASP.NET Core, EF Core 10), SQL Server (SQLite en desarrollo), WPF + MaterialDesignInXaml, React 19 + MUI 7 +
  Vite, SignalR, AWS (S3, SQS, RDS, CloudWatch, IAM), xUnit, Playwright.
- **Arquitectura**: monolito modular con Clean Architecture `Domain ← Application ← Infrastructure ← API / Worker`, contratos en
  `FlowDesk.Shared` ([arquitectura](../docs/architecture/README.md)).
- **Idioma**: documentación, mensajes e interfaz en español; código, base de datos y valores de API (estados, permisos) en inglés.
- **REST**: prefijo `/api`, JSON camelCase, errores `application/problem+json` (RFC 7807) con `code` y `correlationId`,
  paginación `page` (desde 1) / `pageSize` (máx. 100), instantes UTC ISO 8601.
- **Cabeceras**: `Authorization: Bearer <JWT>`, `X-Correlation-Id` (entrada opcional, salida siempre), `X-Internal-Key` solo en el relé interno.
- **Concurrencia**: los comandos sobre un expediente envían su `concurrencyStamp`; si no coincide, 409.
- **Valores estables** (se persisten, no se renombran): estados (`Received` … `Rejected`), códigos de transición (`start-validation` …),
  permisos (`expedients.read` …), acciones de auditoría (`expedient.transition` …), orígenes (`Web`, `DesktopFile`, `Scanner`).
- **Diseño**: Material Design con la paleta sapote `#D9734E`, gris `#EEF0F3` y negro azulado `#1B2533`, compartida entre
  `frontend/packages/ui-components/src/theme.ts` y `desktop/FlowDesk.Desktop/Themes/FlowDeskTheme.xaml`.
