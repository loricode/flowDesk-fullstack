# Captura de escritorio (FlowDesk Capture)

Implementación: [`desktop/FlowDesk.Desktop`](../../../desktop/FlowDesk.Desktop) — módulo `Capture/`
([`DocumentCapture.cs`](../../../desktop/FlowDesk.Desktop/Capture/DocumentCapture.cs),
[`FileCaptureService`](../../../desktop/FlowDesk.Desktop/Capture/FileCaptureService.cs),
[`HotFolderWatcher`](../../../desktop/FlowDesk.Desktop/Capture/HotFolderWatcher.cs),
[`Twain/`](../../../desktop/FlowDesk.Desktop/Capture/Twain/README.md)), `MainViewModel`, `FlowDeskApiClient`.

## Requisito: orígenes de captura desacoplados
Todo origen DEBE implementar `IDocumentCaptureService` (`Id`, `DisplayName`, `Kind`, `IsAvailable`, `UnavailableReason`,
`CaptureAsync(CaptureRequest)`) y devolver `DocumentCaptureResult` (`Success`, `PartialSuccess`, `Cancelled`, `Failed`) con
`CapturedDocument` (nombre, tipo, bytes, origen, páginas). Los ViewModels NO DEBEN conocer implementaciones concretas de escáner.

| Origen | Implementación | Estado |
|---|---|---|
| Archivos (botones Cargar PDF, Cargar imágenes, Seleccionar documentos) | `FileCaptureService` + `IFileDialogService` | Activo |
| Arrastrar y soltar sobre la zona de captura | `FileCaptureService.ImportAsync` | Activo |
| "Abrir con" (rutas como argumentos) | `App.ShowMain` → `ImportAsync` | Activo |
| Carpeta vigilada (`Capture:HotFolder:Path`) | `HotFolderWatcher` → `ImportAsync`; mueve lo importado a `procesados/` | Activo si se configura |
| Escáner TWAIN | `TwainCaptureService` + `ITwainSession` | `None` (deshabilitado) · `Simulated` · `Native` (pendiente) |

## Requisito: validación previa
Antes de agregar un archivo a la bandeja, la app DEBE aplicar `FileSignatureValidator` leyendo solo la cabecera. Los rechazos
DEBEN mostrarse con el nombre y el motivo, sin interrumpir la importación de los demás archivos.

### Escenario: lote con un archivo falso
- **Dado** `contrato-servicios.pdf`, `factura-proveedor.png` y `cedula.pdf` (con contenido `MZ…`)
- **Cuando** llegan a la carpeta vigilada
- **Entonces** los dos primeros entran a la bandeja como "Listo para enviar"
- **Y** se muestra "cedula.pdf: El contenido del archivo no corresponde con su extensión."

## Requisito: expediente destino
La barra lateral DEBE listar solo expedientes en `Received`, `InValidation` o `CorrectionRequested`, con búsqueda. Con
`expedients.create`, el usuario PUEDE crear uno desde un diálogo (título, solicitante, descripción, prioridad, responsable).

## Requisito: vista previa
PDF DEBE mostrarse en WebView2 (desde un archivo temporal que se borra al cerrar) e imágenes en un control de imagen. WebView2 DEBE
ocultarse mientras haya un diálogo abierto (airspace).

## Requisito: envío y resultado
"Enviar" DEBE subir cada documento pendiente con progreso, `source = DesktopFile` o `Scanner`, y consultar
`GET /api/documents/{id}` cada 1,5 s (máx. 40 veces) hasta `Validated` o `Invalid`, mostrando el mensaje del servidor.
Un documento que falla al subir queda "Error de envío" y se reintenta en el siguiente envío.

## Requisito: escáner TWAIN
`TwainCaptureService` DEBE: rechazar si la sesión no está disponible, mostrando el motivo; construir `ScanSettings` desde
`Capture:Twain` (fuente, DPI, color, dúplex, alimentador, UI del driver); adquirir las páginas; tratar la cancelación como
`Cancelled`; unir las páginas en un TIFF multipágina LZW; validarlo y devolverlo con `source = Scanner` y el número de páginas.

### Escenario: sin escáner configurado
- **Dado** `Capture:Twain:Driver = None`
- **Entonces** el botón Escanear está deshabilitado y su tooltip dice "No hay un escáner TWAIN configurado. Por ahora cargue PDF o imágenes."

### Escenario: simulador
- **Dado** `Capture:Twain:Driver = Simulated`
- **Cuando** el operador pulsa Escanear
- **Entonces** recibe `escaneo-aaaammdd-hhmmss.tiff` de 2 páginas con origen "Escáner", listo para enviar
