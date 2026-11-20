# ADR 0004 · Captura documental desacoplada (WPF + TWAIN)

**Estado:** aceptada

## Contexto
El navegador no puede acceder a escáneres. La captura física necesita una aplicación de escritorio,
pero el MVP debe funcionar sin hardware y la integración TWAIN no debe acoplarse al negocio.

## Decisión
- **WPF** es la aplicación de captura; React nunca habla con TWAIN.
- `IDocumentCaptureService` es el único contrato que conocen los ViewModels:
  - `FileCaptureService`: PDF e imágenes (activo).
  - `TwainCaptureService`: configuración, adquisición, TIFF multipágina, validación y errores. Delega
    el protocolo en `ITwainSession` (`Unavailable`, `Simulated` o la futura `Native`).
- La validación de formato (`FileSignatureValidator`) vive en `FlowDesk.Shared` y la usan WPF, API y Worker.
- Todo documento capturado llega al mismo endpoint REST con un `source` (`DesktopFile`, `Scanner`).

## Consecuencias
- Para conectar un escáner real basta con implementar `ITwainSession` y cambiar la configuración.
- El simulador permite demostrar y probar el flujo de escaneo completo sin hardware.
- La app es de 64 bits; con drivers solo de 32 bits se compila en x86 o se usa un proceso puente.
