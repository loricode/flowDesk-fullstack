import { createApiClient } from '@flowdesk/api-client';
import { getStoredToken } from '@flowdesk/auth';

export const API_URL: string = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

export const api = createApiClient({
  baseUrl: API_URL,
  getToken: getStoredToken,
  onUnauthorized: () => {
    sessionStorage.removeItem('flowdesk.session');
    window.location.assign('/login');
  },
});
