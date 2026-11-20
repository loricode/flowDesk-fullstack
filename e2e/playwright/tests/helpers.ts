import { expect, type APIRequestContext, type Page } from '@playwright/test';

export const API_URL = process.env.E2E_API_URL ?? 'http://localhost:5080';

export const users = {
  operator: { email: 'operador@flowdesk.local', password: 'Operador123!', name: 'Óscar Operador' },
  validator: { email: 'validador@flowdesk.local', password: 'Validador123!', name: 'Valeria Validadora' },
  approver: { email: 'aprobador@flowdesk.local', password: 'Aprobador123!', name: 'Andrés Aprobador' },
  auditor: { email: 'auditor@flowdesk.local', password: 'Auditor123!', name: 'Laura Auditora' },
  admin: { email: 'admin@flowdesk.local', password: 'Admin123!', name: 'Ana Administradora' },
} as const;

export type DemoUser = (typeof users)[keyof typeof users];

export async function login(page: Page, user: DemoUser) {
  await page.goto('/login');
  await page.getByLabel('Correo electrónico').fill(user.email);
  await page.getByLabel('Contraseña').fill(user.password);
  await page.getByRole('button', { name: 'Ingresar' }).click();
  await expect(page.getByText(user.name).first()).toBeVisible();
}

export async function logout(page: Page) {
  await page.evaluate(() => sessionStorage.clear());
}

export async function apiToken(request: APIRequestContext, user: DemoUser): Promise<string> {
  const response = await request.post(`${API_URL}/api/auth/login`, { data: { email: user.email, password: user.password } });
  expect(response.ok()).toBeTruthy();
  return (await response.json()).accessToken as string;
}

/** PDF mínimo válido con contenido único (evita la detección de duplicados entre ejecuciones). */
export function samplePdf(label: string): Buffer {
  const text = `FlowDesk E2E ${label} ${Date.now()}`;
  const stream = `BT /F1 18 Tf 72 720 Td (${text}) Tj ET`;
  return Buffer.from(
    `%PDF-1.4
1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj
2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj
3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]/Contents 4 0 R/Resources<</Font<</F1 5 0 R>>>>>>endobj
4 0 obj<</Length ${stream.length}>>stream
${stream}
endstream endobj
5 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj
trailer<</Root 1 0 R>>
%%EOF`,
  );
}
