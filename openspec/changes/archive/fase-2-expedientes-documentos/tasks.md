# Tareas · Fase 2 · Expedientes y documentos

## 1. Expedientes
- [x] 2.1 HU-07 · `Expedient.Create` con reglas RE-01 a RE-03, código único y evento `ExpedientCreated`
- [x] 2.2 HU-08 · Búsqueda paginada con filtros (estado múltiple, prioridad, texto, asignados a mí) y orden por prioridad
- [x] 2.3 HU-09 · Asignación con validación de usuario activo y prohibición en estados finales
- [x] 2.4 HU-10 · `ConcurrencyStamp` como token de concurrencia y verificación del stamp enviado (409)
- [x] 2.5 Pantallas Bandeja de trabajo y Nuevo expediente (web)

## 2. Documentos
- [x] 2.6 HU-11 · `POST /api/expedients/{id}/documents` (multipart, 21 MB máx. de petición)
- [x] 2.7 HU-12 · `FileSignatureValidator`: extensión, tamaño y firma binaria (PDF, PNG, JPG, TIFF)
- [x] 2.8 HU-13 · Versionado por nombre de archivo; el documento vuelve a `Pending`
- [x] 2.9 `IFileStorage` con `LocalFileStorage` (anti path traversal) y `S3FileStorage` (SSE-AES256); compensación si falla la base
- [x] 2.10 HU-15 · `DocumentProcessingService`: SHA-256, revalidación, duplicados por expediente, idempotente
- [x] 2.11 HU-14 · Visor (PDF, imagen, TIFF descargable), selector de versión, hash y autor; consulta auditada

## 3. Pruebas
- [x] 2.12 `ExpedientWorkflowTests` de alta, versionado, documentos en estados cerrados y rotación del stamp
- [x] 2.13 `FileSignatureValidatorTests` (5) y `DocumentProcessingTests` (4) sobre SQLite en memoria
