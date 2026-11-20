import { useState } from 'react';
import { useNavigate } from 'react-router';
import { Alert, Button, Dialog, DialogActions, DialogContent, DialogTitle, MenuItem, Stack, TextField } from '@mui/material';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Permissions, PriorityLabels, type ExpedientPriority } from '@flowdesk/shared-types';
import { api } from '../api';
import { useNotifier } from '../hooks/useNotifier';

export function CreateExpedientDialog({ open, onClose }: { open: boolean; onClose: () => void }) {
  const navigate = useNavigate();
  const notify = useNotifier();
  const queryClient = useQueryClient();
  const [title, setTitle] = useState('');
  const [applicant, setApplicant] = useState('');
  const [description, setDescription] = useState('');
  const [priority, setPriority] = useState<ExpedientPriority>('Normal');
  const [assignee, setAssignee] = useState('');

  const users = useQuery({
    queryKey: ['users', 'lookup', Permissions.WorkflowValidate],
    queryFn: () => api.users.lookup(Permissions.WorkflowValidate),
    enabled: open,
  });

  const create = useMutation({
    mutationFn: () =>
      api.expedients.create({
        title,
        applicantName: applicant,
        description: description || null,
        priority,
        assignedToId: assignee || null,
      }),
    onSuccess: (created) => {
      void queryClient.invalidateQueries({ queryKey: ['expedients'] });
      notify(`Expediente ${created.code} creado.`, 'success');
      onClose();
      navigate(`/expedients/${created.id}`);
    },
  });

  return (
    <Dialog open={open} onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>Nuevo expediente</DialogTitle>
      <DialogContent>
        <Stack spacing={2} mt={1}>
          <TextField label="Título de la solicitud" value={title} onChange={(e) => setTitle(e.target.value)} required slotProps={{ htmlInput: { maxLength: 200 } }} />
          <TextField label="Solicitante" value={applicant} onChange={(e) => setApplicant(e.target.value)} required helperText="Cliente, proveedor o empleado" />
          <TextField label="Descripción" value={description} onChange={(e) => setDescription(e.target.value)} multiline minRows={2} />
          <Stack direction="row" spacing={2}>
            <TextField select label="Prioridad" value={priority} onChange={(e) => setPriority(e.target.value as ExpedientPriority)} fullWidth>
              {(Object.keys(PriorityLabels) as ExpedientPriority[]).map((p) => (
                <MenuItem key={p} value={p}>
                  {PriorityLabels[p]}
                </MenuItem>
              ))}
            </TextField>
            <TextField select label="Responsable" value={assignee} onChange={(e) => setAssignee(e.target.value)} fullWidth>
              <MenuItem value="">Sin asignar</MenuItem>
              {(users.data ?? []).map((u) => (
                <MenuItem key={u.id} value={u.id}>
                  {u.fullName}
                </MenuItem>
              ))}
            </TextField>
          </Stack>
          {create.error && <Alert severity="error">{create.error.message}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} color="inherit">
          Cancelar
        </Button>
        <Button variant="contained" onClick={() => create.mutate()} disabled={!title.trim() || !applicant.trim() || create.isPending}>
          Crear expediente
        </Button>
      </DialogActions>
    </Dialog>
  );
}
