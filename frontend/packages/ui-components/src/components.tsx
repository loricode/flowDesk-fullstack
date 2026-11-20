import type { ReactNode } from 'react';
import { alpha, Box, Card, CardContent, Chip, Stack, Typography } from '@mui/material';
import type { DocumentStatus, ExpedientPriority, ExpedientStatus } from '@flowdesk/shared-types';
import { DocumentStatusLabels, PriorityLabels, StatusLabels } from '@flowdesk/shared-types';
import { palette } from './theme';

const statusColors: Record<ExpedientStatus, string> = {
  Received: palette.info,
  InValidation: palette.sapote.main,
  PendingApproval: palette.warning,
  CorrectionRequested: '#8B5CF6',
  Approved: palette.success,
  Rejected: palette.danger,
};

export const statusColor = (status: ExpedientStatus) => statusColors[status];

function TintChip({ label, color, size = 'small' }: { label: string; color: string; size?: 'small' | 'medium' }) {
  return (
    <Chip
      size={size}
      label={label}
      sx={{ bgcolor: alpha(color, 0.12), color, border: `1px solid ${alpha(color, 0.25)}` }}
    />
  );
}

export function StatusChip({ status, size }: { status: ExpedientStatus; size?: 'small' | 'medium' }) {
  return <TintChip label={StatusLabels[status]} color={statusColors[status]} size={size} />;
}

const priorityColors: Record<ExpedientPriority, string> = {
  Low: palette.gray.text,
  Normal: palette.info,
  High: palette.sapote.main,
  Urgent: palette.danger,
};

export function PriorityChip({ priority }: { priority: ExpedientPriority }) {
  return <TintChip label={PriorityLabels[priority]} color={priorityColors[priority]} />;
}

const documentColors: Record<DocumentStatus, string> = {
  Pending: palette.sapote.main,
  Validated: palette.success,
  Invalid: palette.danger,
};

export function DocumentStatusChip({ status }: { status: DocumentStatus }) {
  return <TintChip label={DocumentStatusLabels[status]} color={documentColors[status]} />;
}

export function PageHeader({
  overline,
  title,
  subtitle,
  actions,
}: {
  overline?: string;
  title: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} alignItems={{ md: 'flex-end' }} justifyContent="space-between" mb={3}>
      <Box>
        {overline && (
          <Typography variant="overline" color="text.secondary">
            {overline}
          </Typography>
        )}
        <Typography variant="h5" component="h1">
          {title}
        </Typography>
        {subtitle && (
          <Typography variant="body2" color="text.secondary" mt={0.5}>
            {subtitle}
          </Typography>
        )}
      </Box>
      {actions && (
        <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>
          {actions}
        </Stack>
      )}
    </Stack>
  );
}

export function KpiCard({
  label,
  value,
  icon,
  accent = palette.sapote.main,
  hint,
}: {
  label: string;
  value: ReactNode;
  icon: ReactNode;
  accent?: string;
  hint?: string;
}) {
  return (
    <Card>
      <CardContent sx={{ display: 'flex', alignItems: 'center', gap: 2, '&:last-child': { pb: 2 } }}>
        <Box
          sx={{
            width: 48,
            height: 48,
            borderRadius: 3,
            display: 'grid',
            placeItems: 'center',
            bgcolor: alpha(accent, 0.12),
            color: accent,
            flexShrink: 0,
          }}
        >
          {icon}
        </Box>
        <Box minWidth={0}>
          <Typography variant="body2" color="text.secondary" noWrap>
            {label}
          </Typography>
          <Typography variant="h5" lineHeight={1.2}>
            {value}
          </Typography>
          {hint && (
            <Typography variant="caption" color="text.secondary">
              {hint}
            </Typography>
          )}
        </Box>
      </CardContent>
    </Card>
  );
}

export function EmptyState({ icon, title, description, action }: { icon: ReactNode; title: string; description?: string; action?: ReactNode }) {
  return (
    <Stack alignItems="center" spacing={1.5} py={6} px={2} textAlign="center">
      <Box
        sx={{
          width: 72,
          height: 72,
          borderRadius: '50%',
          display: 'grid',
          placeItems: 'center',
          bgcolor: palette.gray.background,
          color: palette.sapote.main,
          '& svg': { fontSize: 34 },
        }}
      >
        {icon}
      </Box>
      <Typography variant="subtitle1">{title}</Typography>
      {description && (
        <Typography variant="body2" color="text.secondary" maxWidth={380}>
          {description}
        </Typography>
      )}
      {action}
    </Stack>
  );
}

// ---------- Formateadores ----------

const dateFormat = new Intl.DateTimeFormat('es', { dateStyle: 'medium', timeStyle: 'short' });
const relative = new Intl.RelativeTimeFormat('es', { numeric: 'auto' });

export const formatDate = (iso: string) => dateFormat.format(new Date(iso));

export function formatRelative(iso: string): string {
  const seconds = Math.round((new Date(iso).getTime() - Date.now()) / 1000);
  const abs = Math.abs(seconds);
  if (abs < 60) return relative.format(seconds, 'second');
  if (abs < 3600) return relative.format(Math.round(seconds / 60), 'minute');
  if (abs < 86400) return relative.format(Math.round(seconds / 3600), 'hour');
  return relative.format(Math.round(seconds / 86400), 'day');
}

export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
}
