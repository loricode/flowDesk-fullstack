import { defineConfig, devices } from '@playwright/test';

const WEB_URL = process.env.E2E_WEB_URL ?? 'http://localhost:5173';
const API_URL = process.env.E2E_API_URL ?? 'http://localhost:5080';

/**
 * Pruebas E2E del flujo completo. Si la API y la web no están corriendo, Playwright las levanta:
 * la API en modo "todo en uno" (Worker:RunInApi) con una base SQLite y almacenamiento propios.
 */
export default defineConfig({
  testDir: './tests',
  timeout: 60_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: WEB_URL,
    locale: 'es-ES',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } } }],
  webServer: [
    {
      command: 'dotnet run --project ../../backend/FlowDesk.API --launch-profile http',
      url: `${API_URL}/health`,
      reuseExistingServer: true,
      timeout: 180_000,
      env: {
        Worker__RunInApi: 'true',
        RateLimiting__LoginPermitsPerMinute: '1000',
        ConnectionStrings__FlowDesk: 'Data Source=../.data/e2e/flowdesk-e2e.db',
        Storage__LocalRootPath: '../.data/e2e/storage',
      },
    },
    {
      command: 'npm run dev --prefix ../../frontend',
      url: WEB_URL,
      reuseExistingServer: true,
      timeout: 120_000,
    },
  ],
});

export { API_URL };
