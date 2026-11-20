# Gestión documental

Implementación: [`Document`](../../../backend/FlowDesk.Domain/Documents/Document.cs),
[`DocumentService` y `DocumentProcessingService`](../../../backend/FlowDesk.Application/Documents/DocumentService.cs),
[`FileSignatureValidator`](../../../backend/FlowDesk.Shared/Documents/FileSignatureValidator.cs) (compartido con WPF y el Worker),
[`FileStorage.cs`](../../../backend/FlowDesk.Infrastructure/Storage/FileStorage.cs).

## Endpoints

| Método | Ruta | Permiso | Respuesta |
|---|---|---|---|
| POST | `/api/expedients/{id}/documents` (multipart: `file`, `source`) | `documents.upload` | 201 `DocumentDto` · 400 · 404 · 409 |
| GET | `/api/documents/{id}` | `expedients.read` | 200 `DocumentDto` |
| GET | `/api/documents/{id}/content?version=` | `expedients.read` | 200 binario (rangos habilitados) · 404 |

## Requisito: validación del archivo
Un archivo DEBE tener extensión `.pdf`, `.png`, `.jpg`, `.jpeg`, `.tif` o `.tiff`, tamaño entre 1 byte y 20 MB, y su firma binaria
DEBE coincidir con la extensión (`%PDF`, `89 50 4E 47…`, `FF D8 FF`, `49 49 2A 00` / `4D 4D 00 2A`). La misma regla se aplica en
FlowDesk Capture antes de enviar, en la API al recibir y en el Worker al procesar.

### Escenario: archivo disfrazado
- **Dado** un archivo `factura.pdf` cuyo contenido empieza con `MZ`
- **Cuando** se carga
- **Entonces** recibe 400 "El contenido del archivo no corresponde con su extensión."
- **Y** no se guarda nada en el almacenamiento ni en la base

## Requisito: estados del expediente que admiten documentos
Solo `Received`, `InValidation` y `CorrectionRequested` admiten documentos; en otro estado la carga recibe 400.

## Requisito: versionado
Un archivo con el mismo nombre (sin distinguir mayúsculas) en el mismo expediente DEBE crear una nueva `DocumentVersion`
(`CurrentVersion + 1`) y devolver el documento a `Pending`. Las versiones anteriores siguen consultables.

## Requisito: almacenamiento
El binario DEBE guardarse con la clave `expedients/{expedientId}/{guid}{ext}` en S3 (`Storage:Provider = S3`, SSE-AES256) o en disco
(`Local`, con protección contra path traversal). La base guarda clave, tamaño, tipo, origen, autor, fecha y SHA-256.
Si la metadata no se puede guardar, el objeto DEBE eliminarse (compensación).

## Requisito: procesamiento asíncrono
La carga DEBE emitir `DocumentUploaded` (Outbox). El Worker DEBE:
1. ignorar el mensaje si el documento ya no está `Pending` o la versión no es la actual (idempotencia);
2. calcular el SHA-256 y revalidar la firma;
3. marcar `Invalid` con "Documento duplicado: el contenido es idéntico a '<nombre>'" si otro documento del mismo expediente tiene el mismo hash;
4. si no, marcar `Validated`; auditar `document.processed` y notificar (ver [tiempo real](../tiempo-real-notificaciones/spec.md)).

### Escenario: duplicado en el expediente
- **Dado** `original.pdf` validado en el expediente
- **Cuando** se carga `copia.pdf` con el mismo contenido y el Worker lo procesa
- **Entonces** `copia.pdf` queda `Invalid` con un mensaje que menciona `original.pdf`

## Requisito: visor
La web DEBE descargar el contenido con el token (sin URLs públicas) y mostrarlo con un object URL: PDF en iframe, PNG/JPG como imagen,
TIFF como descarga. Cada consulta DEBE auditarse como `document.downloaded`.
