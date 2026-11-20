import { createContext, useCallback, useContext, useState, type ReactNode } from 'react';
import { Alert, Snackbar, type AlertColor } from '@mui/material';

interface Toast {
  key: number;
  message: string;
  severity: AlertColor;
}

const NotifierContext = createContext<(message: string, severity?: AlertColor) => void>(() => {});

/** Mensajes breves (snackbar) disponibles en toda la aplicación. */
export function NotifierProvider({ children }: { children: ReactNode }) {
  const [toast, setToast] = useState<Toast | null>(null);

  const notify = useCallback((message: string, severity: AlertColor = 'info') => {
    setToast({ key: Date.now(), message, severity });
  }, []);

  return (
    <NotifierContext.Provider value={notify}>
      {children}
      <Snackbar
        key={toast?.key}
        open={toast !== null}
        autoHideDuration={4500}
        onClose={() => setToast(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
      >
        <Alert severity={toast?.severity ?? 'info'} variant="filled" onClose={() => setToast(null)} sx={{ minWidth: 320 }}>
          {toast?.message}
        </Alert>
      </Snackbar>
    </NotifierContext.Provider>
  );
}

export const useNotifier = () => useContext(NotifierContext);
