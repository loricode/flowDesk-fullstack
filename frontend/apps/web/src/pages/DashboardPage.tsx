import { Box, Card, CardContent, Skeleton, Stack, Tooltip, Typography } from '@mui/material';
import FolderOpenOutlined from '@mui/icons-material/FolderOpenOutlined';
import AssignmentIndOutlined from '@mui/icons-material/AssignmentIndOutlined';
import PendingActionsOutlined from '@mui/icons-material/PendingActionsOutlined';
import CheckCircleOutline from '@mui/icons-material/CheckCircleOutline';
import HighlightOff from '@mui/icons-material/HighlightOff';
import TimerOutlined from '@mui/icons-material/TimerOutlined';
import { useQuery } from '@tanstack/react-query';
import { useAuth } from '@flowdesk/auth';
import { StatusLabels, type ExpedientStatus } from '@flowdesk/shared-types';
import { formatRelative, KpiCard, PageHeader, palette, statusColor } from '@flowdesk/ui-components';
import { api } from '../api';
import { describeAction } from './auditLabels';

const statusOrder: ExpedientStatus[] = ['Received', 'InValidation', 'CorrectionRequested', 'PendingApproval', 'Approved', 'Rejected'];

export function DashboardPage() {
  const { user } = useAuth();
  const { data, isLoading } = useQuery({ queryKey: ['dashboard'], queryFn: api.reporting.dashboard, refetchInterval: 60_000 });

  const total = data ? statusOrder.reduce((acc, s) => acc + (data.byStatus[s] ?? 0), 0) : 0;

  return (
    <>
      <PageHeader
        overline="Dashboard operativo"
        title={`Hola, ${user?.fullName.split(' ')[0]}`}
        subtitle="Indicadores de los expedientes y actividad reciente. Se actualiza en tiempo real."
      />

      <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr', lg: 'repeat(3, 1fr)', xl: 'repeat(6, 1fr)' } }}>
        {isLoading || !data ? (
          Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} variant="rounded" height={88} />)
        ) : (
          <>
            <KpiCard label="Expedientes abiertos" value={data.openExpedients} icon={<FolderOpenOutlined />} />
            <KpiCard label="Asignados a mí" value={data.assignedToMe} icon={<AssignmentIndOutlined />} accent={palette.ink.main} />
            <KpiCard label="Pendientes de aprobación" value={data.pendingApproval} icon={<PendingActionsOutlined />} accent={palette.warning} />
            <KpiCard label="Aprobados (30 días)" value={data.approvedLast30Days} icon={<CheckCircleOutline />} accent={palette.success} />
            <KpiCard label="Rechazados (30 días)" value={data.rejectedLast30Days} icon={<HighlightOff />} accent={palette.danger} />
            <KpiCard
              label="Tiempo medio de resolución"
              value={data.averageResolutionHours === null ? '—' : `${data.averageResolutionHours} h`}
              icon={<TimerOutlined />}
              accent={palette.info}
            />
          </>
        )}
      </Box>

      <Box sx={{ display: 'grid', gap: 2, mt: 2, gridTemplateColumns: { xs: '1fr', lg: '5fr 7fr' } }}>
        <Card>
          <CardContent>
            <Typography variant="subtitle1">Expedientes por estado</Typography>
            <Typography variant="body2" color="text.secondary" mb={2.5}>
              {total} expedientes en total
            </Typography>

            {/* Barra apilada: una sola lectura de la distribución del flujo */}
            <Stack direction="row" sx={{ height: 12, borderRadius: 6, overflow: 'hidden', bgcolor: palette.gray.background, gap: '2px' }}>
              {data &&
                total > 0 &&
                statusOrder
                  .filter((s) => data.byStatus[s] > 0)
                  .map((s) => (
                    <Tooltip key={s} title={`${StatusLabels[s]}: ${data.byStatus[s]}`}>
                      <Box sx={{ flex: data.byStatus[s], bgcolor: statusColor(s) }} />
                    </Tooltip>
                  ))}
            </Stack>

            <Stack mt={2.5} spacing={1.25}>
              {statusOrder.map((s) => (
                <Stack key={s} direction="row" alignItems="center" spacing={1.5}>
                  <Box sx={{ width: 10, height: 10, borderRadius: '3px', bgcolor: statusColor(s) }} />
                  <Typography variant="body2" flex={1}>
                    {StatusLabels[s]}
                  </Typography>
                  <Typography variant="body2" fontWeight={700}>
                    {data?.byStatus[s] ?? 0}
                  </Typography>
                  <Typography variant="caption" color="text.secondary" width={40} textAlign="right">
                    {total > 0 && data ? `${Math.round((data.byStatus[s] / total) * 100)}%` : '—'}
                  </Typography>
                </Stack>
              ))}
            </Stack>
          </CardContent>
        </Card>

        <Card>
          <CardContent>
            <Typography variant="subtitle1">Actividad reciente</Typography>
            <Typography variant="body2" color="text.secondary" mb={1.5}>
              Últimas operaciones registradas en la auditoría
            </Typography>
            <Stack divider={<Box sx={{ borderTop: `1px solid ${palette.gray.border}` }} />}>
              {(data?.recentActivity ?? []).map((a) => (
                <Stack key={a.id} direction="row" spacing={2} py={1.25} alignItems="center">
                  <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: palette.sapote.main, flexShrink: 0 }} />
                  <Box flex={1} minWidth={0}>
                    <Typography variant="body2" fontWeight={600}>
                      {describeAction(a.action)}
                    </Typography>
                    <Typography variant="body2" color="text.secondary" noWrap>
                      {a.userName ?? 'Sistema'} · {a.details ?? a.entityType}
                    </Typography>
                  </Box>
                  <Typography variant="caption" color="text.secondary" flexShrink={0}>
                    {formatRelative(a.occurredAt)}
                  </Typography>
                </Stack>
              ))}
              {data && data.recentActivity.length === 0 && (
                <Typography variant="body2" color="text.secondary" py={2}>
                  Aún no hay actividad.
                </Typography>
              )}
            </Stack>
          </CardContent>
        </Card>
      </Box>
    </>
  );
}
