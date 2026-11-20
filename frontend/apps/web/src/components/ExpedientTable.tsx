import { useNavigate } from 'react-router';
import { Box, LinearProgress, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow, Typography } from '@mui/material';
import AttachFileOutlined from '@mui/icons-material/AttachFileOutlined';
import type { ExpedientSummaryDto, PagedResult } from '@flowdesk/shared-types';
import { formatRelative, PriorityChip, StatusChip } from '@flowdesk/ui-components';

/** Tabla de expedientes paginada en servidor, reutilizada por la bandeja y por aprobaciones. */
export function ExpedientTable({
  data,
  loading,
  page,
  pageSize,
  onPageChange,
  onPageSizeChange,
}: {
  data: PagedResult<ExpedientSummaryDto> | undefined;
  loading: boolean;
  page: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
}) {
  const navigate = useNavigate();

  return (
    <>
      <Box sx={{ height: 3 }}>{loading && <LinearProgress />}</Box>
      <TableContainer>
        <Table size="medium">
          <TableHead>
            <TableRow>
              <TableCell>Expediente</TableCell>
              <TableCell>Solicitante</TableCell>
              <TableCell>Estado</TableCell>
              <TableCell>Prioridad</TableCell>
              <TableCell>Responsable</TableCell>
              <TableCell align="center">Docs.</TableCell>
              <TableCell align="right">Actualizado</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {(data?.items ?? []).map((e) => (
              <TableRow key={e.id} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/expedients/${e.id}`)} data-testid="expedient-row">
                <TableCell>
                  <Typography variant="caption" fontWeight={700} color="primary.dark">
                    {e.code}
                  </Typography>
                  <Typography variant="body2" fontWeight={600}>
                    {e.title}
                  </Typography>
                </TableCell>
                <TableCell>{e.applicantName}</TableCell>
                <TableCell>
                  <StatusChip status={e.status} />
                </TableCell>
                <TableCell>
                  <PriorityChip priority={e.priority} />
                </TableCell>
                <TableCell>
                  {e.assignedToName ?? (
                    <Typography variant="body2" color="text.secondary">
                      Sin asignar
                    </Typography>
                  )}
                </TableCell>
                <TableCell align="center">
                  <Box display="inline-flex" alignItems="center" gap={0.5} color="text.secondary">
                    <AttachFileOutlined fontSize="small" />
                    {e.documentCount}
                  </Box>
                </TableCell>
                <TableCell align="right">
                  <Typography variant="body2" color="text.secondary">
                    {formatRelative(e.updatedAt)}
                  </Typography>
                </TableCell>
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
        rowsPerPageOptions={[10, 20, 50]}
        onPageChange={(_, p) => onPageChange(p + 1)}
        onRowsPerPageChange={(e) => onPageSizeChange(Number(e.target.value))}
        labelRowsPerPage="Filas por página"
        labelDisplayedRows={({ from, to, count }) => `${from}–${to} de ${count}`}
      />
    </>
  );
}
