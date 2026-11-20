import { useState } from 'react';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { Permissions, StatusLabels, type TransitionDto } from '@flowdesk/shared-types';
import { StatusChip } from '@flowdesk/ui-components';
import { api } from '../api';

const isNegative = (t: TransitionDto) => t.toStatus === 'Rejected' || t.toStatus === 'CorrectionRequested';

export function TransitionDialog({
  transition,
  pending,
  error,
  onConfirm,
  onClose,
}: {
  transition: TransitionDto;
  pending: boolean;
  error: string | null;
  onConfirm: (comment: string | null) => void;
  onClose: () => void;
}) {
  const [comment, setComment] = useState('');
  const missing = transition.requiresComment && !comment.trim();

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>{transition.name}</DialogTitle>
      <DialogContent>
        <Stack direction="row" spacing={1} alignItems="center" mb={2}>
          <StatusChip status={transition.fromStatus} />
          <Typography color="text.secondary">→</Typography>
          <StatusChip status={transition.toStatus} />
        </Stack>
        <TextField
          label={transition.requiresComment ? 'Observación (obligatoria)' : 'Observación (opcional)'}
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          multiline
          minRows={3}
          fullWidth
          autoFocus
          helperText={`Quedará registrada en el historial al pasar a «${StatusLabels[transition.toStatus]}».`}
        />
        {error && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {error}
          </Alert>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} color="inherit">
          Cancelar
        </Button>
        <Button
          variant="contained"
          color={isNegative(transition) ? 'error' : 'primary'}
          disabled={missing || pending}
          onClick={() => onConfirm(comment.trim() || null)}
        >
          Confirmar
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export function AssignDialog({
  currentId,
  pending,
  error,
  onConfirm,
  onClose,
}: {
  currentId: string | null;
  pending: boolean;
  error: string | null;
  onConfirm: (userId: string) => void;
  onClose: () => void;
}) {
  const [userId, setUserId] = useState(currentId ?? '');
  const users = useQuery({
    queryKey: ['users', 'lookup', 'all-readers'],
    queryFn: () => api.users.lookup(Permissions.ExpedientsRead),
  });

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="xs">
      <DialogTitle>Asignar responsable</DialogTitle>
      <DialogContent>
        <TextField select label="Responsable" value={userId} onChange={(e) => setUserId(e.target.value)} fullWidth sx={{ mt: 1 }}>
          {(users.data ?? []).map((u) => (
            <MenuItem key={u.id} value={u.id}>
              {u.fullName} <Typography component="span" variant="caption" color="text.secondary" ml={1}>{u.roles.join(', ')}</Typography>
            </MenuItem>
          ))}
        </TextField>
        {error && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {error}
          </Alert>
        )}
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} color="inherit">
          Cancelar
        </Button>
        <Button variant="contained" disabled={!userId || pending} onClick={() => onConfirm(userId)}>
          Asignar
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export { isNegative };
