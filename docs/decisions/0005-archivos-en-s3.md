# ADR 0005 · Archivos en S3, metadata en SQL Server

**Estado:** aceptada

## Decisión
Los binarios se guardan en S3 (cifrado SSE, versionado, acceso público bloqueado). SQL Server
guarda la referencia (`StorageKey`), el tamaño, el tipo, el hash SHA-256, el origen y el número de versión.
Los archivos se descargan siempre a través de la API (con autorización y auditoría), nunca con URLs públicas.

## Consecuencias
- La base de datos se mantiene pequeña; backups y restauraciones rápidos.
- S3 escala sin intervención y el ciclo de vida mueve las versiones antiguas a almacenamiento barato.
- Se compensa el caso "archivo subido pero metadata no guardada" eliminando el objeto.
- `IFileStorage` permite usar disco local en desarrollo.
