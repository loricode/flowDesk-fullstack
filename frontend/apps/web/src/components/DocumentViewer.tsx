import { useEffect, useState } from 'react';
import { Alert, Box, CircularProgress, Stack, Typography } from '@mui/material';
import type { DocumentDto } from '@flowdesk/shared-types';
import { palette } from '@flowdesk/ui-components';
import { api } from '../api';

/**
 * Visor documental: descarga el archivo con el token (no hay URLs públicas) y lo muestra con un
 * object URL. PDF en iframe, PNG/JPG como imagen; TIFF no lo renderizan los navegadores.
 */
export function DocumentViewer({ document, version }: { document: DocumentDto; version: number }) {
  const [url, setUrl] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let objectUrl: string | null = null;
    let cancelled = false;
    setUrl(null);
    setError(null);

    api.documents
      .content(document.id, version)
      .then((blob) => {
        if (cancelled) return;
        objectUrl = URL.createObjectURL(blob);
        setUrl(objectUrl);
      })
      .catch((e: Error) => !cancelled && setError(e.message));

    return () => {
      cancelled = true;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [document.id, version]);

  const frame = { width: '100%', height: '100%', border: 0, borderRadius: 8, background: '#fff' } as const;

  return (
    <Box sx={{ height: 560, bgcolor: palette.gray.background, borderRadius: 3, p: 1.5, display: 'grid', placeItems: 'center' }}>
      {error && <Alert severity="error">{error}</Alert>}
      {!error && !url && <CircularProgress />}
      {url && document.contentType === 'application/pdf' && <iframe title={document.fileName} src={url} style={frame} />}
      {url && (document.contentType === 'image/png' || document.contentType === 'image/jpeg') && (
        <Box component="img" src={url} alt={document.fileName} sx={{ maxWidth: '100%', maxHeight: '100%', objectFit: 'contain', borderRadius: 2, boxShadow: 1 }} />
      )}
      {url && document.contentType === 'image/tiff' && (
        <Stack alignItems="center" spacing={1}>
          <Typography variant="body2" color="text.secondary">
            Los navegadores no muestran TIFF (formato de escaneo multipágina).
          </Typography>
          <a href={url} download={document.fileName}>
            Descargar {document.fileName}
          </a>
        </Stack>
      )}
    </Box>
  );
}
