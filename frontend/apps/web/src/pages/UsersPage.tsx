import { useState } from 'react';
import {
  Alert,
  Avatar,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  Chip,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  FormGroup,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from '@mui/material';
import PersonAddOutlined from '@mui/icons-material/PersonAddOutlined';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { RoleDto, UserDto } from '@flowdesk/shared-types';
import { PageHeader, palette } from '@flowdesk/ui-components';
import { api } from '../api';
import { useNotifier } from '../hooks/useNotifier';

export function UsersPage() {
  const users = useQuery({ queryKey: ['users'], queryFn: api.users.list });
  const roles = useQuery({ queryKey: ['roles'], queryFn: api.users.roles });
  const [editing, setEditing] = useState<UserDto | 'new' | null>(null);

  return (
    <>
      <PageHeader
        overline="Identity"
        title="Usuarios y roles"
        subtitle="Autorización basada en roles y permisos. El backend valida cada permiso en cada operación."
        actions={
          <Button variant="contained" startIcon={<PersonAddOutlined />} onClick={() => setEditing('new')}>
            Nuevo usuario
          </Button>
        }
      />

      <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', xl: '2fr 1fr' } }}>
        <Card>
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Usuario</TableCell>
                <TableCell>Roles</TableCell>
                <TableCell>Estado</TableCell>
                <TableCell />
              </TableRow>
            </TableHead>
            <TableBody>
              {(users.data ?? []).map((u) => (
                <TableRow key={u.id} hover>
                  <TableCell>
                    <Stack direction="row" spacing={1.5} alignItems="center">
                      <Avatar sx={{ bgcolor: palette.ink.main, width: 34, height: 34, fontSize: 13 }}>
                        {u.fullName.split(' ').slice(0, 2).map((p) => p[0]).join('')}
                      </Avatar>
                      <Box>
                        <Typography variant="body2" fontWeight={600}>
                          {u.fullName}
                        </Typography>
                        <Typography variant="caption" color="text.secondary">
                          {u.email}
                        </Typography>
                      </Box>
                    </Stack>
                  </TableCell>
                  <TableCell>
                    <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap>
                      {u.roles.map((r) => (
                        <Chip key={r} size="small" label={r} variant="outlined" />
                      ))}
                    </Stack>
                  </TableCell>
                  <TableCell>
                    <Chip size="small" label={u.isActive ? 'Activo' : 'Inactivo'} color={u.isActive ? 'success' : 'default'} variant="outlined" />
                  </TableCell>
                  <TableCell align="right">
                    <Button size="small" onClick={() => setEditing(u)}>
                      Editar
                    </Button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </Card>

        <Card>
          <CardContent>
            <Typography variant="subtitle1" mb={1.5}>
              Matriz de permisos
            </Typography>
            <Stack spacing={1.5}>
              {(roles.data ?? []).map((r) => (
                <Box key={r.name}>
                  <Typography variant="body2" fontWeight={700}>
                    {r.name}{' '}
                    <Typography component="span" variant="caption" color="text.secondary">
                      {r.description}
                    </Typography>
                  </Typography>
                  <Stack direction="row" spacing={0.5} mt={0.5} flexWrap="wrap" useFlexGap>
                    {r.permissions.map((p) => (
                      <Chip key={p} size="small" label={p} sx={{ fontFamily: 'monospace', fontSize: 11 }} />
                    ))}
                  </Stack>
                </Box>
              ))}
            </Stack>
          </CardContent>
        </Card>
      </Box>

      {editing && <UserDialog user={editing === 'new' ? null : editing} roles={roles.data ?? []} onClose={() => setEditing(null)} />}
    </>
  );
}

function UserDialog({ user, roles, onClose }: { user: UserDto | null; roles: RoleDto[]; onClose: () => void }) {
  const notify = useNotifier();
  const queryClient = useQueryClient();
  const [email, setEmail] = useState('');
  const [fullName, setFullName] = useState('');
  const [password, setPassword] = useState('');
  const [selected, setSelected] = useState<string[]>(user?.roles ?? []);
  const [active, setActive] = useState(user?.isActive ?? true);

  const save = useMutation({
    mutationFn: () =>
      user ? api.users.update(user.id, { roles: selected, isActive: active }) : api.users.create({ email, fullName, password, roles: selected }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['users'] });
      notify(user ? 'Usuario actualizado.' : 'Usuario creado.', 'success');
      onClose();
    },
  });

  const toggle = (name: string) => setSelected((s) => (s.includes(name) ? s.filter((x) => x !== name) : [...s, name]));

  return (
    <Dialog open onClose={onClose} fullWidth maxWidth="sm">
      <DialogTitle>{user ? `Editar ${user.fullName}` : 'Nuevo usuario'}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} mt={1}>
          {!user && (
            <>
              <TextField label="Nombre completo" value={fullName} onChange={(e) => setFullName(e.target.value)} />
              <TextField label="Correo" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
              <TextField label="Contraseña inicial" type="password" value={password} onChange={(e) => setPassword(e.target.value)} helperText="Mínimo 8 caracteres" />
            </>
          )}
          <Box>
            <Typography variant="body2" fontWeight={600} mb={0.5}>
              Roles
            </Typography>
            <FormGroup row>
              {roles.map((r) => (
                <FormControlLabel key={r.name} control={<Checkbox checked={selected.includes(r.name)} onChange={() => toggle(r.name)} />} label={r.name} />
              ))}
            </FormGroup>
          </Box>
          {user && <FormControlLabel control={<Switch checked={active} onChange={(e) => setActive(e.target.checked)} />} label="Usuario activo" />}
          {save.error && <Alert severity="error">{save.error.message}</Alert>}
        </Stack>
      </DialogContent>
      <DialogActions sx={{ px: 3, pb: 2.5 }}>
        <Button onClick={onClose} color="inherit">
          Cancelar
        </Button>
        <Button variant="contained" onClick={() => save.mutate()} disabled={save.isPending || selected.length === 0}>
          Guardar
        </Button>
      </DialogActions>
    </Dialog>
  );
}
