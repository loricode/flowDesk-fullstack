import { useState, type FormEvent } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router';
import { Alert, Box, Button, Chip, CircularProgress, InputAdornment, Stack, TextField, Typography } from '@mui/material';
import EmailOutlined from '@mui/icons-material/EmailOutlined';
import LockOutlined from '@mui/icons-material/LockOutlined';
import DescriptionRounded from '@mui/icons-material/DescriptionRounded';
import BoltOutlined from '@mui/icons-material/BoltOutlined';
import VerifiedUserOutlined from '@mui/icons-material/VerifiedUserOutlined';
import AccountTreeOutlined from '@mui/icons-material/AccountTreeOutlined';
import { useAuth } from '@flowdesk/auth';
import { palette } from '@flowdesk/ui-components';
import { api } from '../api';

const demoUsers = [
  { email: 'operador@flowdesk.local', password: 'Operador123!', label: 'Operador' },
  { email: 'validador@flowdesk.local', password: 'Validador123!', label: 'Validador' },
  { email: 'aprobador@flowdesk.local', password: 'Aprobador123!', label: 'Aprobador' },
  { email: 'auditor@flowdesk.local', password: 'Auditor123!', label: 'Auditor' },
  { email: 'admin@flowdesk.local', password: 'Admin123!', label: 'Admin' },
];

export function LoginPage() {
  const { isAuthenticated, signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (isAuthenticated) return <Navigate to="/" replace />;

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      signIn(await api.auth.login(email, password));
      const from = (location.state as { from?: string } | null)?.from ?? '/';
      navigate(from, { replace: true });
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Box sx={{ minHeight: '100vh', display: 'grid', gridTemplateColumns: { xs: '1fr', md: '1fr 1fr' } }}>
      <Box
        sx={{
          bgcolor: palette.ink.main,
          color: '#fff',
          p: 6,
          display: { xs: 'none', md: 'flex' },
          flexDirection: 'column',
          justifyContent: 'space-between',
          position: 'relative',
          overflow: 'hidden',
        }}
      >
        <Box
          sx={{
            position: 'absolute',
            width: 520,
            height: 520,
            borderRadius: '50%',
            right: -180,
            bottom: -200,
            background: `radial-gradient(circle, ${palette.sapote.main}55 0%, transparent 70%)`,
          }}
        />
        <Stack direction="row" spacing={1.5} alignItems="center">
          <Box sx={{ width: 44, height: 44, borderRadius: 3, bgcolor: palette.sapote.main, display: 'grid', placeItems: 'center' }}>
            <DescriptionRounded />
          </Box>
          <Typography fontWeight={800} fontSize={22}>
            FlowDesk
          </Typography>
        </Stack>

        <Box maxWidth={460} position="relative">
          <Typography variant="h4" fontWeight={700} lineHeight={1.2}>
            Automatice la captura, validación y aprobación de documentos.
          </Typography>
          <Typography mt={2} sx={{ color: palette.ink.muted }}>
            Una plataforma para centralizar expedientes, controlar el workflow y mantener trazabilidad completa de cada operación.
          </Typography>
          <Stack mt={4} spacing={1.5}>
            {[
              { icon: <AccountTreeOutlined />, text: 'Motor de workflow con transiciones controladas' },
              { icon: <BoltOutlined />, text: 'Procesamiento asíncrono y notificaciones en tiempo real' },
              { icon: <VerifiedUserOutlined />, text: 'Permisos por rol y auditoría inalterable' },
            ].map((f) => (
              <Stack key={f.text} direction="row" spacing={1.5} alignItems="center">
                <Box sx={{ color: palette.sapote.main, display: 'flex' }}>{f.icon}</Box>
                <Typography fontSize={15}>{f.text}</Typography>
              </Stack>
            ))}
          </Stack>
        </Box>

        <Typography fontSize={12} sx={{ color: palette.ink.muted }}>
          Enterprise Document &amp; Workflow Management Platform
        </Typography>
      </Box>

      <Box sx={{ display: 'grid', placeItems: 'center', p: 3 }}>
        <Box component="form" onSubmit={submit} sx={{ width: '100%', maxWidth: 400 }}>
          <Typography variant="h5">Iniciar sesión</Typography>
          <Typography color="text.secondary" mt={0.5} mb={3}>
            Use sus credenciales corporativas.
          </Typography>

          <Stack spacing={2}>
            <TextField
              label="Correo electrónico"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              autoComplete="username"
              required
              fullWidth
              slotProps={{ input: { startAdornment: <InputAdornment position="start"><EmailOutlined /></InputAdornment> } }}
            />
            <TextField
              label="Contraseña"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
              required
              fullWidth
              slotProps={{ input: { startAdornment: <InputAdornment position="start"><LockOutlined /></InputAdornment> } }}
            />
            {error && <Alert severity="error">{error}</Alert>}
            <Button type="submit" variant="contained" size="large" disabled={busy} startIcon={busy ? <CircularProgress size={18} color="inherit" /> : null}>
              Ingresar
            </Button>
          </Stack>

          <Box mt={4}>
            <Typography variant="caption" color="text.secondary">
              Usuarios de demostración
            </Typography>
            <Stack direction="row" spacing={1} mt={1} flexWrap="wrap" useFlexGap>
              {demoUsers.map((u) => (
                <Chip
                  key={u.email}
                  label={u.label}
                  variant="outlined"
                  onClick={() => {
                    setEmail(u.email);
                    setPassword(u.password);
                  }}
                />
              ))}
            </Stack>
          </Box>
        </Box>
      </Box>
    </Box>
  );
}
