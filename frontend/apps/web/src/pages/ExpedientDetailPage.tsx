import { useEffect, useRef, useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import {
  Alert,
  alpha,
  Box,
  Breadcrumbs,
  Button,
  Card,
  CardContent,
  Chip,
  Divider,
  Link,
  List,
  ListItemButton,
  ListItemText,
  MenuItem,
  Skeleton,
  Stack,
  Tab,
  Tabs,
  TextField,
  Tooltip,
  Typography,
} from '@mui/material';
import UploadFileOutlined from '@mui/icons-material/UploadFileOutlined';
import PersonAddAltOutlined from '@mui/icons-material/PersonAddAltOutlined';
import PictureAsPdfOutlined from '@mui/icons-material/PictureAsPdfOutlined';
import ImageOutlined from '@mui/icons-material/ImageOutlined';
import ScannerOutlined from '@mui/icons-material/ScannerOutlined';
import ArrowForwardRounded from '@mui/icons-material/ArrowForwardRounded';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Can, useAuth } from '@flowdesk/auth';
import {
  Permissions,
  SourceLabels,
  StatusLabels,
  type DocumentDto,
  type ExpedientDetailDto,
  type TransitionDto,
} from '@flowdesk/shared-types';
import {
  DocumentStatusChip,
  EmptyState,
  formatBytes,
  formatDate,
  formatRelative,
  palette,
  PriorityChip,
  StatusChip,
  statusColor,
} from '@flowdesk/ui-components';
import { api } from '../api';
import { DocumentViewer } from '../components/DocumentViewer';
import { AssignDialog, isNegative, TransitionDialog } from '../components/WorkflowDialogs';
import { useNotifier } from '../hooks/useNotifier';
import { useExpedientChannel } from '../hooks/useRealtime';
import { describeAction } from './auditLabels';

const ACCEPTS_DOCUMENTS = ['Received', 'InValidation', 'CorrectionRequested'];

export function ExpedientDetailPage() {
  const { id = '' } = useParams();
  const { can } = useAuth();
  const notify = useNotifier();
  const queryClient = useQueryClient();
  const fileInput = useRef<HTMLInputElement>(null);
  const [tab, setTab] = useState(0);
  const [transition, setTransition] = useState<TransitionDto | null>(null);
  const [assigning, setAssigning] = useState(false);

  useExpedientChannel(id);

  const { data: e, isLoading, error } = useQuery({ queryKey: ['expedient', id], queryFn: () => api.expedients.get(id) });

  const setDetail = (detail: ExpedientDetailDto) => {
    queryClient.setQueryData(['expedient', id], detail);
    void queryClient.invalidateQueries({ queryKey: ['expedients'] });
  };

  const transitionMutation = useMutation({
    mutationFn: ({ code, comment }: { code: string; comment: string | null }) =>
      api.expedients.transition(id, code, comment, e!.concurrencyStamp),
    onSuccess: (detail) => {
      setDetail(detail);
      setTransition(null);
      notify(`Expediente en estado «${StatusLabels[detail.status]}».`, 'success');
    },
  });

  const assignMutation = useMutation({
    mutationFn: (userId: string) => api.expedients.assign(id, userId, e!.concurrencyStamp),
    onSuccess: (detail) => {
      setDetail(detail);
      setAssigning(false);
      notify(`Asignado a ${detail.assignedToName}.`, 'success');
    },
  });

  const uploadMutation = useMutation({
    mutationFn: async (files: File[]) => {
      for (const file of files) await api.expedients.upload(id, file);
    },
    onSuccess: (_, files) => {
      void queryClient.invalidateQueries({ queryKey: ['expedient', id] });
      notify(`${files.length} documento(s) cargado(s). Procesando…`, 'info');
    },
    onError: (err: Error) => notify(err.message, 'error'),
  });

  if (isLoading) return <Skeleton variant="rounded" height={480} />;
  if (error || !e) return <Alert severity="error">{error?.message ?? 'Expediente no encontrado.'}</Alert>;

  const canUpload = can(Permissions.DocumentsUpload) && ACCEPTS_DOCUMENTS.includes(e.status);

  return (
    <>
      <Breadcrumbs sx={{ mb: 1 }}>
        <Link component={RouterLink} to="/expedients" underline="hover" color="text.secondary">
          Bandeja de trabajo
        </Link>
        <Typography color="text.primary">{e.code}</Typography>
      </Breadcrumbs>

      {/* Encabezado */}
      <Card sx={{ mb: 2, borderTop: `4px solid ${statusColor(e.status)}` }}>
        <CardContent>
          <Stack direction={{ xs: 'column', lg: 'row' }} spacing={2} justifyContent="space-between">
            <Box>
              <Stack direction="row" spacing={1} alignItems="center" mb={0.5}>
                <Typography variant="overline" color="primary.dark">
                  {e.code}
                </Typography>
                <StatusChip status={e.status} />
                <PriorityChip priority={e.priority} />
              </Stack>
              <Typography variant="h5">{e.title}</Typography>
              <Typography color="text.secondary" mt={0.5}>
                {e.applicantName} · creado por {e.createdByName} {formatRelative(e.createdAt)} · responsable:{' '}
                <b>{e.assignedToName ?? 'sin asignar'}</b>
              </Typography>
              {e.description && (
                <Typography variant="body2" mt={1.5} maxWidth={760}>
                  {e.description}
                </Typography>
              )}
            </Box>

            <Stack direction="row" spacing={1} alignItems="flex-start" flexWrap="wrap" useFlexGap>
              <Can permission={Permissions.ExpedientsAssign}>
                <Button variant="outlined" color="secondary" startIcon={<PersonAddAltOutlined />} onClick={() => setAssigning(true)}>
                  Asignar
                </Button>
              </Can>
              {canUpload && (
                <>
                  <input
                    ref={fileInput}
                    type="file"
                    hidden
                    multiple
                    accept=".pdf,.png,.jpg,.jpeg,.tif,.tiff"
                    data-testid="upload-input"
                    onChange={(ev) => {
                      const files = Array.from(ev.target.files ?? []);
                      if (files.length) uploadMutation.mutate(files);
                      ev.target.value = '';
                    }}
                  />
                  <Button variant="outlined" startIcon={<UploadFileOutlined />} onClick={() => fileInput.current?.click()} disabled={uploadMutation.isPending}>
                    Cargar documentos
                  </Button>
                </>
              )}
              {e.availableTransitions.map((t) => (
                <Button
                  key={t.code}
                  variant={isNegative(t) ? 'outlined' : 'contained'}
                  color={isNegative(t) ? 'error' : 'primary'}
                  endIcon={isNegative(t) ? undefined : <ArrowForwardRounded />}
                  onClick={() => {
                    transitionMutation.reset();
                    setTransition(t);
                  }}
                >
                  {t.name}
                </Button>
              ))}
            </Stack>
          </Stack>
        </CardContent>
      </Card>

      <Card>
        <Tabs value={tab} onChange={(_, v: number) => setTab(v)} sx={{ px: 2, borderBottom: `1px solid ${palette.gray.border}` }}>
          <Tab label={`Documentos (${e.documents.length})`} />
          <Tab label={`Historial (${e.history.length})`} />
          <Tab label={`Aprobaciones (${e.approvals.length})`} />
          {can(Permissions.AuditRead) && <Tab label="Auditoría" />}
        </Tabs>
        <CardContent>
          {tab === 0 && <DocumentsTab documents={e.documents} />}
          {tab === 1 && <HistoryTab expedient={e} />}
          {tab === 2 && <ApprovalsTab expedient={e} />}
          {tab === 3 && <AuditTab expedientId={e.id} documentIds={e.documents.map((d) => d.id)} />}
        </CardContent>
      </Card>

      {transition && (
        <TransitionDialog
          transition={transition}
          pending={transitionMutation.isPending}
          error={transitionMutation.error?.message ?? null}
          onConfirm={(comment) => transitionMutation.mutate({ code: transition.code, comment })}
          onClose={() => setTransition(null)}
        />
      )}
      {assigning && (
        <AssignDialog
          currentId={e.assignedToId}
          pending={assignMutation.isPending}
          error={assignMutation.error?.message ?? null}
          onConfirm={(userId) => assignMutation.mutate(userId)}
          onClose={() => setAssigning(false)}
        />
      )}
    </>
  );
}

function sourceIcon(d: DocumentDto) {
  if (d.source === 'Scanner') return <ScannerOutlined />;
  return d.contentType === 'application/pdf' ? <PictureAsPdfOutlined /> : <ImageOutlined />;
}

function DocumentsTab({ documents }: { documents: DocumentDto[] }) {
  const [selectedId, setSelectedId] = useState<string | null>(documents[0]?.id ?? null);
  const selected = documents.find((d) => d.id === selectedId) ?? documents[0];
  const [version, setVersion] = useState<number>(selected?.currentVersion ?? 1);

  useEffect(() => {
    if (selected) setVersion(selected.currentVersion);
  }, [selected?.id, selected?.currentVersion]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!selected)
    return (
      <EmptyState
        icon={<UploadFileOutlined />}
        title="Sin documentos"
        description="Cárguelos desde aquí o digitalícelos con FlowDesk Capture (aplicación de escritorio)."
      />
    );

  const v = selected.versions.find((x) => x.version === version) ?? selected.versions[0];

  return (
    <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', lg: '340px 1fr' } }}>
      <List disablePadding>
        {documents.map((d) => (
          <ListItemButton
            key={d.id}
            selected={d.id === selected.id}
            onClick={() => setSelectedId(d.id)}
            sx={{
              borderRadius: 2.5,
              mb: 1,
              border: `1px solid ${palette.gray.border}`,
              alignItems: 'flex-start',
              '&.Mui-selected': { borderColor: palette.sapote.main, bgcolor: alpha(palette.sapote.main, 0.06) },
            }}
          >
            <Box sx={{ color: palette.sapote.main, mr: 1.5, mt: 0.5, display: 'flex' }}>{sourceIcon(d)}</Box>
            <ListItemText
              primary={d.fileName}
              slotProps={{ primary: { fontWeight: 600, noWrap: true } }}
              secondary={
                <Stack spacing={0.75} mt={0.5} component="span">
                  <Typography variant="caption" component="span" color="text.secondary">
                    v{d.currentVersion} · {formatBytes(d.sizeBytes)} · {SourceLabels[d.source]}
                  </Typography>
                  <span>
                    <DocumentStatusChip status={d.status} />
                  </span>
                  {d.validationMessage && (
                    <Typography variant="caption" component="span" color="error.main">
                      {d.validationMessage}
                    </Typography>
                  )}
                </Stack>
              }
            />
          </ListItemButton>
        ))}
      </List>

      <Box minWidth={0}>
        <Stack direction="row" spacing={2} alignItems="center" mb={1.5} flexWrap="wrap" useFlexGap>
          <Typography variant="subtitle1" flex={1} noWrap>
            {selected.fileName}
          </Typography>
          {selected.versions.length > 1 && (
            <TextField select size="small" label="Versión" value={version} onChange={(ev) => setVersion(Number(ev.target.value))} sx={{ width: 140 }}>
              {selected.versions.map((x) => (
                <MenuItem key={x.version} value={x.version}>
                  v{x.version}
                  {x.version === selected.currentVersion ? ' (actual)' : ''}
                </MenuItem>
              ))}
            </TextField>
          )}
        </Stack>
        <DocumentViewer document={selected} version={version} />
        {v && (
          <Stack direction="row" spacing={3} mt={1.5} flexWrap="wrap" useFlexGap>
            <Typography variant="caption" color="text.secondary">
              Cargado por <b>{v.uploadedByName}</b> · {formatDate(v.uploadedAt)}
            </Typography>
            <Tooltip title={v.sha256 ?? 'Pendiente de procesamiento'}>
              <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'monospace' }}>
                SHA-256: {v.sha256 ? `${v.sha256.slice(0, 16)}…` : 'procesando'}
              </Typography>
            </Tooltip>
          </Stack>
        )}
      </Box>
    </Box>
  );
}

function HistoryTab({ expedient }: { expedient: ExpedientDetailDto }) {
  return (
    <Box sx={{ position: 'relative', pl: 3 }}>
      <Box sx={{ position: 'absolute', left: 8, top: 6, bottom: 6, width: 2, bgcolor: palette.gray.border }} />
      {[...expedient.history].reverse().map((h, i) => (
        <Box key={i} sx={{ position: 'relative', pb: 2.5 }}>
          <Box
            sx={{
              position: 'absolute',
              left: -22,
              top: 4,
              width: 12,
              height: 12,
              borderRadius: '50%',
              bgcolor: statusColor(h.toStatus),
              boxShadow: `0 0 0 4px ${alpha(statusColor(h.toStatus), 0.18)}`,
            }}
          />
          <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
            {h.fromStatus && (
              <>
                <StatusChip status={h.fromStatus} />
                <Typography color="text.secondary">→</Typography>
              </>
            )}
            <StatusChip status={h.toStatus} />
            <Typography variant="body2" color="text.secondary">
              {h.changedByName} · {formatDate(h.occurredAt)}
            </Typography>
          </Stack>
          {h.comment && (
            <Typography variant="body2" mt={0.75} sx={{ bgcolor: palette.gray.background, borderRadius: 2, px: 1.5, py: 1, display: 'inline-block' }}>
              {h.comment}
            </Typography>
          )}
        </Box>
      ))}
    </Box>
  );
}

const decisionLabels = { Approved: 'Aprobado', Rejected: 'Rechazado', CorrectionRequested: 'Devuelto para corrección' };
const decisionColors = { Approved: palette.success, Rejected: palette.danger, CorrectionRequested: '#8B5CF6' };

function ApprovalsTab({ expedient }: { expedient: ExpedientDetailDto }) {
  if (expedient.approvals.length === 0)
    return <EmptyState icon={<ArrowForwardRounded />} title="Sin decisiones aún" description="Las decisiones de aprobación quedarán registradas aquí." />;

  return (
    <Stack divider={<Divider />}>
      {expedient.approvals.map((a, i) => (
        <Stack key={i} direction="row" spacing={2} py={1.5} alignItems="center">
          <Chip label={decisionLabels[a.decision]} sx={{ bgcolor: alpha(decisionColors[a.decision], 0.12), color: decisionColors[a.decision] }} />
          <Box flex={1}>
            <Typography variant="body2" fontWeight={600}>
              {a.decidedByName}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {a.comment ?? 'Sin observaciones'}
            </Typography>
          </Box>
          <Typography variant="caption" color="text.secondary">
            {formatDate(a.decidedAt)}
          </Typography>
        </Stack>
      ))}
    </Stack>
  );
}

function AuditTab({ expedientId, documentIds }: { expedientId: string; documentIds: string[] }) {
  const ids = [expedientId, ...documentIds];
  const { data } = useQuery({
    queryKey: ['audit', 'expedient', ids],
    queryFn: async () => (await Promise.all(ids.map((entityId) => api.reporting.audit({ entityId, pageSize: 100 })))).flatMap((r) => r.items),
  });
  const rows = (data ?? []).sort((a, b) => b.occurredAt.localeCompare(a.occurredAt));

  return (
    <Stack divider={<Divider />}>
      {rows.map((a) => (
        <Stack key={a.id} direction="row" spacing={2} py={1.25}>
          <Typography variant="caption" color="text.secondary" width={150} flexShrink={0}>
            {formatDate(a.occurredAt)}
          </Typography>
          <Box flex={1}>
            <Typography variant="body2" fontWeight={600}>
              {describeAction(a.action)}
              {a.previousState && a.newState && (
                <Typography component="span" variant="body2" color="text.secondary">
                  {' '}
                  · {a.previousState} → {a.newState}
                </Typography>
              )}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {a.userName ?? 'Sistema'} {a.details ? `· ${a.details}` : ''}
            </Typography>
          </Box>
          <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'monospace' }}>
            {a.correlationId?.slice(0, 8)}
          </Typography>
        </Stack>
      ))}
    </Stack>
  );
}
