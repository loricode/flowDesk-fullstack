# Tareas · Fase 4 · Escritorio y TWAIN

## 1. Aplicación
- [x] 4.1 HU-25 · Host genérico con DI, `FlowDeskApiClient` (ProblemDetails → mensajes), sesión y login
- [x] 4.2 HU-25 · Barra lateral con expedientes que admiten documentos, búsqueda y diálogo Nuevo expediente
- [x] 4.3 HU-26 · `FileCaptureService` (botones con filtros), arrastrar y soltar, "Abrir con" (argumentos)
- [x] 4.4 HU-26 · `HotFolderWatcher`: carpeta vigilada configurable que mueve lo importado a `procesados/`
- [x] 4.5 HU-27 · Validación previa con `FileSignatureValidator` y mensaje por archivo rechazado
- [x] 4.6 HU-28 · Vista previa: WebView2 para PDF (carpeta de datos en LocalAppData) e imágenes; manejo de airspace
- [x] 4.7 HU-29 · Envío con progreso y seguimiento del procesamiento hasta Validado / Inválido

## 2. TWAIN
- [x] 4.8a HU-30 · `TwainCaptureService`: configuración, adquisición, cancelación, TIFF multipágina LZW, validación y errores
- [x] 4.8b HU-30 · `ITwainSession` con `UnavailableTwainSession` y `SimulatedTwainSession`; selección por `Capture:Twain:Driver`
- [ ] 4.8 HU-30 · `NativeTwainSession` sobre el DSM (NTwain o P/Invoke) probada con un escáner real (ver `Capture/Twain/README.md`)

## 3. Pruebas
- [x] 4.10 Recorrido automatizado con UI Automation (`docs/presentacion/video/escritorio.ps1`): login, alta, carpeta vigilada, rechazo, vista previa, simulador, envío
- [ ] 4.9 HU-26 · Probar de punta a punta la carga por cuadro de diálogo y por arrastrar y soltar
- [ ] 4.11 Proyecto de pruebas del escritorio: `FileCaptureService` (con `IFileDialogService` falso), `TwainCaptureService` (con sesión falsa) y `MainViewModel`
