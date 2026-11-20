# Fase 2 · Expedientes y documentos

## Por qué
El expediente y sus documentos son el centro del negocio: sin un agregado consistente y una gestión documental confiable
(formato real, versiones, archivos fuera de la base) no hay flujo que controlar.

## Qué cambia
- Agregado `Expedient` con documentos, historial y aprobaciones; concurrencia optimista.
- Bandeja con filtros y paginación; asignación de responsable.
- Carga de documentos con validación de firma, versionado por nombre, almacenamiento S3/local con compensación.
- Procesamiento asíncrono: hash SHA-256, revalidación y duplicados.
- Visor documental en la web.

## Specs afectadas
[expedientes](../../../specs/expedientes/spec.md), [gestion-documental](../../../specs/gestion-documental/spec.md)

## Historias
HU-07 a HU-15.
