import { useState, type ReactNode } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router';
import {
  alpha,
  Avatar,
  Badge,
  Box,
  Divider,
  IconButton,
  List,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Menu,
  MenuItem,
  Stack,
  Tooltip,
  Typography,
} from '@mui/material';
import DashboardOutlined from '@mui/icons-material/DashboardOutlined';
import InboxOutlined from '@mui/icons-material/InboxOutlined';
import TaskAltOutlined from '@mui/icons-material/TaskAltOutlined';
import HistoryOutlined from '@mui/icons-material/HistoryOutlined';
import GroupOutlined from '@mui/icons-material/GroupOutlined';
import NotificationsOutlined from '@mui/icons-material/NotificationsOutlined';
import LogoutOutlined from '@mui/icons-material/LogoutOutlined';
import DescriptionRounded from '@mui/icons-material/DescriptionRounded';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useAuth } from '@flowdesk/auth';
import { Permissions } from '@flowdesk/shared-types';
import { formatRelative, palette } from '@flowdesk/ui-components';
import { api } from '../api';
import { useRealtime, type RealtimeStatus } from '../hooks/useRealtime';

const SIDEBAR_WIDTH = 256;

interface NavItem {
  to: string;
  label: string;
  icon: ReactNode;
  permission?: string;
}

const navItems: NavItem[] = [
  { to: '/', label: 'Dashboard', icon: <DashboardOutlined /> },
  { to: '/expedients', label: 'Bandeja de trabajo', icon: <InboxOutlined /> },
  { to: '/approvals', label: 'Aprobaciones', icon: <TaskAltOutlined />, permission: Permissions.WorkflowApprove },
  { to: '/audit', label: 'Auditoría', icon: <HistoryOutlined />, permission: Permissions.AuditRead },
  { to: '/users', label: 'Usuarios y roles', icon: <GroupOutlined />, permission: Permissions.UsersManage },
];

const realtimeLabel: Record<RealtimeStatus, { text: string; color: string }> = {
  connected: { text: 'Tiempo real activo', color: palette.success },
  connecting: { text: 'Conectando…', color: palette.warning },
  reconnecting: { text: 'Reconectando…', color: palette.warning },
  disconnected: { text: 'Sin conexión en tiempo real', color: palette.danger },
};

export function AppLayout() {
  const { user, can, signOut } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const realtime = useRealtime();
  const [userMenu, setUserMenu] = useState<HTMLElement | null>(null);
  const [bellMenu, setBellMenu] = useState<HTMLElement | null>(null);

  const notifications = useQuery({ queryKey: ['notifications'], queryFn: () => api.notifications.list() });
  const unread = notifications.data?.filter((n) => !n.isRead).length ?? 0;

  const initials = (user?.fullName ?? '?')
    .split(' ')
    .slice(0, 2)
    .map((p) => p[0])
    .join('')
    .toUpperCase();

  const openBell = async (anchor: HTMLElement) => {
    setBellMenu(anchor);
    if (unread > 0) {
      await api.notifications.readAll();
      void queryClient.invalidateQueries({ queryKey: ['notifications'] });
    }
  };

  const logout = () => {
    signOut();
    queryClient.clear();
    navigate('/login');
  };

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      {/* Barra lateral: negro azulado */}
      <Box
        component="nav"
        sx={{
          width: SIDEBAR_WIDTH,
          flexShrink: 0,
          bgcolor: palette.ink.main,
          color: '#fff',
          display: 'flex',
          flexDirection: 'column',
          position: 'sticky',
          top: 0,
          height: '100vh',
        }}
      >
        <Stack direction="row" alignItems="center" spacing={1.5} px={2.5} py={2.5}>
          <Box sx={{ width: 38, height: 38, borderRadius: 2.5, bgcolor: palette.sapote.main, display: 'grid', placeItems: 'center' }}>
            <DescriptionRounded sx={{ color: '#fff' }} />
          </Box>
          <Box>
            <Typography fontWeight={800} fontSize={18} lineHeight={1.1}>
              FlowDesk
            </Typography>
            <Typography fontSize={11} fontWeight={600} sx={{ color: palette.sapote.light, letterSpacing: '0.06em' }}>
              DOCUMENT &amp; WORKFLOW
            </Typography>
          </Box>
        </Stack>

        <List sx={{ px: 1.5, flex: 1 }}>
          {navItems
            .filter((item) => !item.permission || can(item.permission))
            .map((item) => (
              <ListItemButton
                key={item.to}
                component={NavLink}
                to={item.to}
                end={item.to === '/'}
                sx={{
                  borderRadius: 2.5,
                  mb: 0.5,
                  color: palette.ink.muted,
                  borderLeft: '3px solid transparent',
                  '& .MuiListItemIcon-root': { color: 'inherit', minWidth: 38 },
                  '&:hover': { bgcolor: alpha('#fff', 0.06), color: '#fff' },
                  '&.active': {
                    bgcolor: alpha(palette.sapote.main, 0.16),
                    color: '#fff',
                    borderLeftColor: palette.sapote.main,
                    '& .MuiListItemIcon-root': { color: palette.sapote.main },
                  },
                }}
              >
                <ListItemIcon>{item.icon}</ListItemIcon>
                <ListItemText primary={item.label} slotProps={{ primary: { fontWeight: 600, fontSize: 14 } }} />
              </ListItemButton>
            ))}
        </List>

        <Box px={2.5} py={2}>
          <Stack direction="row" alignItems="center" spacing={1}>
            <Box sx={{ width: 8, height: 8, borderRadius: '50%', bgcolor: realtimeLabel[realtime].color }} />
            <Typography fontSize={12} sx={{ color: palette.ink.muted }}>
              {realtimeLabel[realtime].text}
            </Typography>
          </Stack>
        </Box>
      </Box>

      {/* Contenido */}
      <Box sx={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
        <Box
          component="header"
          sx={{
            height: 64,
            px: 4,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'flex-end',
            gap: 1,
            bgcolor: alpha(palette.gray.background, 0.85),
            backdropFilter: 'blur(8px)',
            borderBottom: `1px solid ${palette.gray.border}`,
            position: 'sticky',
            top: 0,
            zIndex: 10,
          }}
        >
          <Tooltip title="Notificaciones">
            <IconButton onClick={(e) => void openBell(e.currentTarget)} aria-label="Notificaciones">
              <Badge badgeContent={unread} color="primary">
                <NotificationsOutlined />
              </Badge>
            </IconButton>
          </Tooltip>

          <Stack
            direction="row"
            spacing={1.5}
            alignItems="center"
            onClick={(e) => setUserMenu(e.currentTarget)}
            data-testid="user-menu"
            sx={{ cursor: 'pointer', pl: 1, py: 0.5, pr: 1.5, borderRadius: 3, '&:hover': { bgcolor: alpha(palette.ink.main, 0.05) } }}
          >
            <Avatar sx={{ bgcolor: palette.sapote.main, width: 36, height: 36, fontSize: 14, fontWeight: 700 }}>{initials}</Avatar>
            <Box>
              <Typography fontSize={14} fontWeight={600} lineHeight={1.2}>
                {user?.fullName}
              </Typography>
              <Typography fontSize={12} color="text.secondary">
                {user?.roles.join(' · ')}
              </Typography>
            </Box>
          </Stack>
        </Box>

        <Box component="main" sx={{ p: 4, flex: 1, maxWidth: 1480, width: '100%', mx: 'auto' }}>
          <Outlet />
        </Box>
      </Box>

      <Menu anchorEl={userMenu} open={Boolean(userMenu)} onClose={() => setUserMenu(null)}>
        <MenuItem disabled>
          <Typography variant="body2">{user?.email}</Typography>
        </MenuItem>
        <Divider />
        <MenuItem onClick={logout}>
          <ListItemIcon>
            <LogoutOutlined fontSize="small" />
          </ListItemIcon>
          Cerrar sesión
        </MenuItem>
      </Menu>

      <Menu
        anchorEl={bellMenu}
        open={Boolean(bellMenu)}
        onClose={() => setBellMenu(null)}
        slotProps={{ paper: { sx: { width: 380, maxHeight: 440 } } }}
      >
        <Typography px={2} py={1} variant="subtitle2">
          Notificaciones
        </Typography>
        <Divider />
        {(notifications.data ?? []).length === 0 && (
          <Typography px={2} py={2} variant="body2" color="text.secondary">
            No tiene notificaciones.
          </Typography>
        )}
        {(notifications.data ?? []).map((n) => (
          <MenuItem
            key={n.id}
            onClick={() => {
              setBellMenu(null);
              if (n.expedientId) navigate(`/expedients/${n.expedientId}`);
            }}
            sx={{ whiteSpace: 'normal', alignItems: 'flex-start', py: 1.25 }}
          >
            <Box>
              <Typography variant="body2" fontWeight={n.isRead ? 500 : 700}>
                {n.title}
              </Typography>
              <Typography variant="body2" color="text.secondary">
                {n.message}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                {formatRelative(n.createdAt)}
              </Typography>
            </Box>
          </MenuItem>
        ))}
      </Menu>
    </Box>
  );
}
