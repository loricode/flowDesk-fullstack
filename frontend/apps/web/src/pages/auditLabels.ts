const labels: Record<string, string> = {
  'auth.login': 'Inicio de sesión',
  'auth.loginFailed': 'Inicio de sesión fallido',
  'expedient.created': 'Expediente creado',
  'expedient.assigned': 'Expediente asignado',
  'expedient.transition': 'Cambio de estado',
  'document.uploaded': 'Documento cargado',
  'document.downloaded': 'Documento consultado',
  'document.processed': 'Documento procesado',
  'user.created': 'Usuario creado',
  'user.updated': 'Usuario actualizado',
};

export const auditActions = Object.keys(labels);

export const describeAction = (action: string) => labels[action] ?? action;
