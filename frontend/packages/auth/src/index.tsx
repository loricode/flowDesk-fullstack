import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';
import type { LoginResponse, UserDto } from '@flowdesk/shared-types';

const STORAGE_KEY = 'flowdesk.session';

interface StoredSession {
  token: string;
  expiresAt: string;
  user: UserDto;
}

export interface AuthContextValue {
  user: UserDto | null;
  token: string | null;
  isAuthenticated: boolean;
  signIn: (login: LoginResponse) => void;
  signOut: () => void;
  /** Solo para adaptar la UI: el backend valida cada permiso. */
  can: (permission: string) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

function readSession(): StoredSession | null {
  try {
    const raw = sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const session = JSON.parse(raw) as StoredSession;
    return new Date(session.expiresAt) > new Date() ? session : null;
  } catch {
    return null;
  }
}

/** Token actual fuera de React (cliente HTTP, SignalR). */
export function getStoredToken(): string | null {
  return readSession()?.token ?? null;
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<StoredSession | null>(readSession);

  const signIn = useCallback((login: LoginResponse) => {
    const next = { token: login.accessToken, expiresAt: login.expiresAt, user: login.user };
    // sessionStorage: la sesión no sobrevive al cierre del navegador.
    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    setSession(next);
  }, []);

  const signOut = useCallback(() => {
    sessionStorage.removeItem(STORAGE_KEY);
    setSession(null);
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({
      user: session?.user ?? null,
      token: session?.token ?? null,
      isAuthenticated: session !== null,
      signIn,
      signOut,
      can: (permission) => session?.user.permissions.includes(permission) ?? false,
    }),
    [session, signIn, signOut],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth debe usarse dentro de <AuthProvider>.');
  return ctx;
}

/** Renderiza el contenido solo si el usuario tiene el permiso. */
export function Can({ permission, children, fallback = null }: { permission: string; children: ReactNode; fallback?: ReactNode }) {
  const { can } = useAuth();
  return <>{can(permission) ? children : fallback}</>;
}
