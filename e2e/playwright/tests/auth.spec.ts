import { expect, test } from '@playwright/test';
import { API_URL, apiToken, login, users } from './helpers';

test.describe('Inicio de sesión', () => {
  test('rechaza credenciales inválidas', async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('Correo electrónico').fill(users.operator.email);
    await page.getByLabel('Contraseña').fill('incorrecta');
    await page.getByRole('button', { name: 'Ingresar' }).click();
    await expect(page.getByRole('alert')).toContainText('Credenciales inválidas');
  });

  test('redirige a login si no hay sesión', async ({ page }) => {
    await page.goto('/expedients');
    await expect(page).toHaveURL(/\/login$/);
  });

  test('el operador accede al dashboard', async ({ page }) => {
    await login(page, users.operator);
    await expect(page.getByRole('heading', { name: /Hola, Óscar/ })).toBeVisible();
    await expect(page.getByText('Tiempo real activo')).toBeVisible();
  });
});

test.describe('Permisos', () => {
  test('el menú se adapta al rol', async ({ page }) => {
    await login(page, users.operator);
    await expect(page.getByRole('link', { name: 'Bandeja de trabajo' })).toBeVisible();
    await expect(page.getByRole('link', { name: 'Auditoría' })).toHaveCount(0);
    await expect(page.getByRole('link', { name: 'Usuarios y roles' })).toHaveCount(0);
  });

  test('el administrador ve auditoría y usuarios', async ({ page }) => {
    await login(page, users.admin);
    await expect(page.getByRole('link', { name: 'Auditoría' })).toBeVisible();
    await page.getByRole('link', { name: 'Usuarios y roles' }).click();
    await expect(page.getByText('Matriz de permisos')).toBeVisible();
  });

  test('una ruta sin permiso redirige al inicio', async ({ page }) => {
    await login(page, users.operator);
    await page.goto('/audit');
    await expect(page).toHaveURL(/\/$/);
  });

  test('el backend valida permisos aunque la UI se salte', async ({ request }) => {
    const token = await apiToken(request, users.operator);
    const response = await request.get(`${API_URL}/api/audit`, { headers: { Authorization: `Bearer ${token}` } });
    expect(response.status()).toBe(403);
  });
});
