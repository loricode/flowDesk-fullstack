import type {
  AuditEntryDto,
  CreateExpedientRequest,
  CreateUserRequest,
  DashboardDto,
  DocumentDto,
  ExpedientDetailDto,
  ExpedientQuery,
  ExpedientSummaryDto,
  LoginResponse,
  NotificationDto,
  PagedResult,
  ProblemDetails,
  RoleDto,
  UpdateUserRequest,
  UserDto,
  UserLookupDto,
} from '@flowdesk/shared-types';

/** Error de la API con el ProblemDetails (RFC 7807) devuelto por el backend. */
export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails;

  constructor(status: number, problem: ProblemDetails) {
    super(ApiError.describe(status, problem));
    this.status = status;
    this.problem = problem;
  }

  private static describe(status: number, p: ProblemDetails): string {
    if (p.errors) return Object.values(p.errors).flat().join(' ');
    if (p.detail) return p.detail;
    if (status === 401) return 'Sesión expirada o credenciales inválidas.';
    if (status === 403) return 'No tiene permisos para esta operación.';
    if (status === 429) return 'Demasiados intentos. Espere un minuto e intente de nuevo.';
    return p.title ?? `Error ${status}`;
  }
}

export interface ApiClientOptions {
  baseUrl: string;
  getToken: () => string | null;
  onUnauthorized?: () => void;
}

/** Cliente HTTP tipado y reutilizable por cualquier app del monorepo. */
export function createApiClient({ baseUrl, getToken, onUnauthorized }: ApiClientOptions) {
  const root = baseUrl.replace(/\/$/, '');

  async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const headers = new Headers(init.headers);
    const token = getToken();
    if (token) headers.set('Authorization', `Bearer ${token}`);
    if (init.body && !(init.body instanceof FormData)) headers.set('Content-Type', 'application/json');
    headers.set('X-Correlation-Id', crypto.randomUUID().replaceAll('-', ''));

    const response = await fetch(`${root}${path}`, { ...init, headers });

    if (response.status === 401 && token) onUnauthorized?.();
    if (!response.ok) {
      const problem = (await response.json().catch(() => ({}))) as ProblemDetails;
      throw new ApiError(response.status, problem);
    }
    if (response.status === 204) return undefined as T;
    const type = response.headers.get('content-type') ?? '';
    return (type.includes('json') ? response.json() : response.blob()) as Promise<T>;
  }

  const json = (body: unknown) => JSON.stringify(body);
  const qs = (params: Record<string, string | number | boolean | undefined | null>) => {
    const search = new URLSearchParams();
    for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== '') search.set(k, String(v));
    const s = search.toString();
    return s ? `?${s}` : '';
  };

  return {
    baseUrl: root,

    auth: {
      login: (email: string, password: string) =>
        request<LoginResponse>('/api/auth/login', { method: 'POST', body: json({ email, password }) }),
      me: () => request<UserDto>('/api/auth/me'),
    },

    expedients: {
      search: (q: ExpedientQuery) =>
        request<PagedResult<ExpedientSummaryDto>>(`/api/expedients${qs({ ...q })}`),
      get: (id: string) => request<ExpedientDetailDto>(`/api/expedients/${id}`),
      create: (body: CreateExpedientRequest) =>
        request<ExpedientDetailDto>('/api/expedients', { method: 'POST', body: json(body) }),
      assign: (id: string, userId: string, concurrencyStamp: string) =>
        request<ExpedientDetailDto>(`/api/expedients/${id}/assign`, {
          method: 'POST',
          body: json({ userId, concurrencyStamp }),
        }),
      transition: (id: string, transitionCode: string, comment: string | null, concurrencyStamp: string) =>
        request<ExpedientDetailDto>(`/api/expedients/${id}/transitions`, {
          method: 'POST',
          body: json({ transitionCode, comment, concurrencyStamp }),
        }),
      upload: (id: string, file: File) => {
        const form = new FormData();
        form.append('file', file);
        form.append('source', 'Web');
        return request<DocumentDto>(`/api/expedients/${id}/documents`, { method: 'POST', body: form });
      },
    },

    documents: {
      get: (id: string) => request<DocumentDto>(`/api/documents/${id}`),
      content: (id: string, version?: number) =>
        request<Blob>(`/api/documents/${id}/content${qs({ version })}`),
    },

    users: {
      list: () => request<UserDto[]>('/api/users'),
      lookup: (permission?: string) => request<UserLookupDto[]>(`/api/users/lookup${qs({ permission })}`),
      create: (body: CreateUserRequest) => request<UserDto>('/api/users', { method: 'POST', body: json(body) }),
      update: (id: string, body: UpdateUserRequest) =>
        request<UserDto>(`/api/users/${id}`, { method: 'PUT', body: json(body) }),
      roles: () => request<RoleDto[]>('/api/roles'),
    },

    reporting: {
      dashboard: () => request<DashboardDto>('/api/dashboard'),
      audit: (params: { entityId?: string; action?: string; page?: number; pageSize?: number }) =>
        request<PagedResult<AuditEntryDto>>(`/api/audit${qs(params)}`),
    },

    notifications: {
      list: (unreadOnly = false) => request<NotificationDto[]>(`/api/notifications${qs({ unreadOnly })}`),
      readAll: () => request<void>('/api/notifications/read-all', { method: 'POST' }),
    },
  };
}

export type FlowDeskApi = ReturnType<typeof createApiClient>;
