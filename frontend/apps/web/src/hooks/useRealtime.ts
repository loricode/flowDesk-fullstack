import { useEffect, useState } from 'react';
import { HubConnectionBuilder, HubConnectionState, LogLevel, type HubConnection } from '@microsoft/signalr';
import { useQueryClient } from '@tanstack/react-query';
import type { RealtimeEvent } from '@flowdesk/shared-types';
import { useAuth } from '@flowdesk/auth';
import { API_URL } from '../api';
import { useNotifier } from './useNotifier';

let sharedConnection: HubConnection | null = null;

export type RealtimeStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

/**
 * Conexión SignalR (WebSockets) con el hub de notificaciones. Cada evento invalida las consultas
 * afectadas en React Query, de modo que bandejas, tablero y detalle se actualizan sin recargar.
 */
export function useRealtime(): RealtimeStatus {
  const { token, user } = useAuth();
  const queryClient = useQueryClient();
  const notify = useNotifier();
  const [status, setStatus] = useState<RealtimeStatus>('connecting');

  useEffect(() => {
    if (!token) return;

    const connection = new HubConnectionBuilder()
      .withUrl(`${API_URL}/hubs/notifications`, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    sharedConnection = connection;

    connection.on('ReceiveEvent', (event: RealtimeEvent) => {
      queryClient.invalidateQueries({ queryKey: ['expedients'] });
      queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      if (event.expedientId) queryClient.invalidateQueries({ queryKey: ['expedient', event.expedientId] });

      if (event.type === 'notification' && event.targetUserId === user?.id) {
        queryClient.invalidateQueries({ queryKey: ['notifications'] });
        notify(event.message, 'info');
      }
    });

    connection.onreconnecting(() => setStatus('reconnecting'));
    connection.onreconnected(() => setStatus('connected'));
    connection.onclose(() => setStatus('disconnected'));

    let cancelled = false;
    connection
      .start()
      .then(() => !cancelled && setStatus('connected'))
      .catch(() => !cancelled && setStatus('disconnected'));

    return () => {
      cancelled = true;
      sharedConnection = null;
      void connection.stop();
    };
  }, [token, user?.id, queryClient, notify]);

  return status;
}

/** Suscribe la vista de detalle al grupo del expediente mientras está abierta. */
export function useExpedientChannel(expedientId: string | undefined) {
  useEffect(() => {
    const connection = sharedConnection;
    if (!expedientId || !connection) return;

    const join = () => connection.invoke('JoinExpedient', expedientId).catch(() => {});
    if (connection.state === HubConnectionState.Connected) void join();
    connection.onreconnected(() => void join());

    return () => {
      if (connection.state === HubConnectionState.Connected)
        void connection.invoke('LeaveExpedient', expedientId).catch(() => {});
    };
  }, [expedientId]);
}
