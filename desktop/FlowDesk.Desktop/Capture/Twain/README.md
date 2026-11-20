# Integración TWAIN

La captura por escáner está **lista a nivel de arquitectura**. Hoy la app carga PDF e imágenes
(`FileCaptureService`) y la opción de escáner aparece deshabilitada porque
`Capture:Twain:Driver = None`.

```
IDocumentCaptureService
 ├─ FileCaptureService        → PDF / imágenes (activo)
 └─ TwainCaptureService       → configuración, ensamblado TIFF multipágina, validación, errores
       └─ ITwainSession       → único punto que habla con el driver
            ├─ UnavailableTwainSession  (Driver = None)
            ├─ SimulatedTwainSession    (Driver = Simulated)  ← demo sin hardware
            └─ NativeTwainSession       (Driver = Native)     ← por implementar con el escáner
```

## Probar el flujo sin escáner

En `appsettings.json` del escritorio:

```json
"Capture": { "Twain": { "Driver": "Simulated", "Dpi": 300, "ColorMode": "Color", "Duplex": false } }
```

El botón **Escanear** genera páginas sintéticas, las une en un TIFF y las envía como origen `Scanner`.

## Conectar un escáner real

1. Instalar el **TWAIN Data Source Manager** (`twaindsm.dll`, incluido con la mayoría de drivers) y el
   driver TWAIN del fabricante.
2. Agregar una librería TWAIN, p. ej. `dotnet add package NTwain`, o usar P/Invoke sobre `twaindsm.dll`.
3. Crear `NativeTwainSession : ITwainSession`:
   - `IsAvailable`: abrir el DSM (estado 3) y comprobar que existe al menos un origen.
   - `GetSources()`: enumerar los orígenes (`DG_CONTROL / DAT_IDENTITY / MSG_GETFIRST/GETNEXT`).
   - `AcquireAsync(settings)`: abrir el origen (estado 4), negociar capacidades
     (`ICAP_XRESOLUTION`, `ICAP_PIXELTYPE`, `CAP_DUPLEXENABLED`, `CAP_FEEDERENABLED`), habilitarlo
     (estado 5, con o sin UI según `ShowDriverUi`) y, por cada `MSG_XFERREADY`, transferir la imagen
     (nativa o por memoria) convirtiéndola a `BitmapSource`. Terminar con `MSG_ENDXFER` / `MSG_RESET`.
   - Ejecutar todo en el hilo de UI (STA) usando el `Dispatcher`: TWAIN requiere un bucle de mensajes.
4. Registrar la sesión en `App.xaml.cs` dentro de `CreateTwainSession` para `TwainDriver.Native`.
5. Cambiar `Driver` a `Native`.

No hay que tocar ViewModels, vistas, cliente HTTP ni backend: el documento llega al mismo endpoint
`POST /api/expedients/{id}/documents` con `source = Scanner`.

### Notas de arquitectura

- La app es de 64 bits; muchos drivers antiguos solo existen en 32 bits. En ese caso compilar con
  `<PlatformTarget>x86</PlatformTarget>` o usar un proceso puente de 32 bits.
- React nunca habla con TWAIN: el navegador no tiene acceso al hardware. El escritorio captura y la
  API es el único punto de entrada al negocio.
