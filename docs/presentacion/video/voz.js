// Narración por escena: la clave es el título del subtítulo o de la portada (grabar.js).
module.exports = {
  'El camino feliz': 'FlowDesk: el camino feliz. Seguimos un expediente desde que se digitalizan sus documentos hasta que queda aprobado y auditado.',

  // Parte 1 · Escritorio
  'FlowDesk Capture': 'Empezamos en FlowDesk Capture, la aplicación de escritorio para digitalizar documentos. Entra el operador.',
  'Crear el expediente': 'El operador crea el expediente: título, solicitante, descripción y responsable. Nace en estado recibido.',
  'Llegan los documentos': 'Llegan los documentos: un contrato en PDF, una factura en imagen y una cédula que en realidad no es un PDF. La aplicación revisa la firma binaria de cada archivo y rechaza la falsa antes de enviarla.',
  'Revisar antes de enviar': 'Antes de enviar, el operador revisa cada documento en la vista previa.',
  'Preparado para escáneres TWAIN': 'La captura por escáner está lista. Aquí, con el simulador TWAIN: dos páginas digitalizadas y unidas en un solo TIFF.',
  'Enviar al servidor': 'Envía los tres documentos. Cada uno sube con su barra de progreso.',
  'Procesado en segundos': 'En segundos, el Worker calculó el hash de cada archivo, revalidó su formato y comprobó que no hubiera duplicados.',

  // Parte 2 · Web
  'La aplicación web': 'Ahora, la aplicación web, donde el expediente sigue su flujo de validación y aprobación.',
  'Entra la validadora': 'Entra la validadora. Su tablero resume los expedientes abiertos, los pendientes de aprobación y la actividad reciente, y se actualiza en tiempo real.',
  'Su bandeja de trabajo': 'En la bandeja de trabajo están los expedientes, con su estado, su prioridad y su responsable. Ahí está el que acaba de crear el operador.',
  'El expediente completo': 'El detalle muestra los documentos con su origen, su versión y su hash, junto al visor.',
  'El workflow manda': 'El workflow manda: solo aparecen las transiciones que su rol permite. Inicia la validación y lo envía a aprobación.',
  'Entra el aprobador': 'Entra el aprobador. Su bandeja de aprobaciones muestra lo que espera su decisión.',
  'Aprobar con observación': 'Revisa y aprueba, dejando una observación.',
  'Historial completo': 'Cada cambio de estado quedó en el historial, con su responsable, su fecha y su observación, y la decisión en aprobaciones.',
  'El operador fue notificado': 'Mientras tanto, el operador recibió una notificación por cada cambio de estado de su expediente.',
  'Todo queda auditado': 'Por último, la administradora revisa la auditoría: quién hizo qué, cuándo, y con qué identificador de correlación. Este registro no se puede modificar ni borrar.',
  'Usuarios y permisos': 'Y la matriz de roles y permisos, que el backend valida en cada operación. Eso es FlowDesk. Gracias por su tiempo.',
};
