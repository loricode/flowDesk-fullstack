import { useState } from 'react';
import {
  Box,
  Card,
  LinearProgress,
  MenuItem,
  Stack,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { formatDate, PageHeader } from '@flowdesk/ui-components';
import { api } from '../api';
import { auditActions, describeAction } from './auditLabels';

export function AuditPage() {
  const [action, setAction] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const { data, isFetching } = useQuery({
    queryKey: ['audit', action, page, pageSize],
    queryFn: () => api.reporting.audit({ action: action || undefined, page, pageSize }),
    placeholderData: keepPreviousData,
  });

  return (
    <>
      <PageHeader
        overline="Trazabilidad"
        title="Auditoría"
        subtitle="Registro inalterable de operaciones: quién, qué, cuándo (UTC) y con qué identificador de correlación."
      />
      <Card>
        <Stack direction="row" p={2}>
          <TextField
            select
            size="small"
            label="Acción"
            value={action}
            onChange={(e) => {
              setAction(e.target.value);
              setPage(1);
            }}
            sx={{ minWidth: 260 }}
          >
            <MenuItem value="">Todas</MenuItem>
            {auditActions.map((a) => (
              <MenuItem key={a} value={a}>
                {describeAction(a)}
              </MenuItem>
            ))}
          </TextField>
        </Stack>
        <Box sx={{ height: 3 }}>{isFetching && <LinearProgress />}</Box>
        <TableContainer>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Fecha</TableCell>
                <TableCell>Usuario</TableCell>
                <TableCell>Acción</TableCell>
                <TableCell>Entidad</TableCell>
                <TableCell>Cambio</TableCell>
                <TableCell>Detalle</TableCell>
                <TableCell>Correlación</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {(data?.items ?? []).map((a) => (
                <TableRow key={a.id} hover>
                  <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatDate(a.occurredAt)}</TableCell>
                  <TableCell>{a.userName ?? 'Sistema'}</TableCell>
                  <TableCell sx={{ fontWeight: 600 }}>{describeAction(a.action)}</TableCell>
                  <TableCell>
                    <Typography variant="body2">{a.entityType}</Typography>
                    <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'monospace' }}>
                      {a.entityId?.slice(0, 8)}
                    </Typography>
                  </TableCell>
                  <TableCell>{a.previousState || a.newState ? `${a.previousState ?? '∅'} → ${a.newState ?? '∅'}` : '—'}</TableCell>
                  <TableCell sx={{ maxWidth: 320 }}>
                    <Typography variant="body2" noWrap title={a.details ?? ''}>
                      {a.details ?? '—'}
                    </Typography>
                  </TableCell>
                  <TableCell sx={{ fontFamily: 'monospace', fontSize: 12 }}>{a.correlationId?.slice(0, 12)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
        <TablePagination
          component="div"
          count={data?.totalCount ?? 0}
          page={page - 1}
          rowsPerPage={pageSize}
          rowsPerPageOptions={[25, 50, 100]}
          onPageChange={(_, p) => setPage(p + 1)}
          onRowsPerPageChange={(e) => {
            setPageSize(Number(e.target.value));
            setPage(1);
          }}
          labelRowsPerPage="Filas por página"
          labelDisplayedRows={({ from, to, count }) => `${from}–${to} de ${count}`}
        />
      </Card>
    </>
  );
}
