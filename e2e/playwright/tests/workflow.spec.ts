import { expect, test, type Page } from '@playwright/test';
import { login, logout, samplePdf, users } from './helpers';

/**
 * Flujo de negocio completo: creación → carga → procesamiento asíncrono (Worker) →
 * validación → aprobación, con tres usuarios y roles distintos.
 */
test.describe.serial('Flujo completo de un expediente', () => {
  const title = `Alta de proveedor E2E ${Date.now()}`;
  let expedientUrl = '';

  async function openExpedient(page: Page) {
    await page.goto(expedientUrl);
    await expect(page.getByRole('heading', { name: title })).toBeVisible();
  }

  test('el operador crea el expediente y carga un documento', async ({ page }) => {
    await login(page, users.operator);
    await page.getByRole('link', { name: 'Bandeja de trabajo' }).click();
    await page.getByRole('button', { name: 'Nuevo expediente' }).click();

    const dialog = page.getByRole('dialog');
    await dialog.getByLabel('Título de la solicitud').fill(title);
    await dialog.getByLabel('Solicitante').fill('Proveedor E2E S.A.');
    await dialog.getByRole('button', { name: 'Crear expediente' }).click();

    await expect(page.getByRole('heading', { name: title })).toBeVisible();
    await expect(page.getByText('Recibido').first()).toBeVisible();
    expedientUrl = page.url();

    await page.getByTestId('upload-input').setInputFiles({
      name: 'contrato.pdf',
      mimeType: 'application/pdf',
      buffer: samplePdf('contrato'),
    });

    // El Worker procesa el documento y SignalR actualiza la vista sin recargar.
    await expect(page.getByRole('button', { name: /contrato\.pdf/ })).toBeVisible();
    await expect(page.getByText('Validado').first()).toBeVisible({ timeout: 30_000 });
  });

  test('un archivo con firma falsa es rechazado', async ({ page }) => {
    await login(page, users.operator);
    await openExpedient(page);
    await page.getByTestId('upload-input').setInputFiles({
      name: 'falso.pdf',
      mimeType: 'application/pdf',
      buffer: Buffer.from('MZ esto no es un PDF'),
    });
    await expect(page.getByRole('alert')).toContainText('no corresponde con su extensión');
  });

  test('el operador no puede validar', async ({ page }) => {
    await login(page, users.operator);
    await openExpedient(page);
    await expect(page.getByRole('button', { name: 'Iniciar validación' })).toHaveCount(0);
  });

  test('la validadora revisa y envía a aprobación', async ({ page }) => {
    await login(page, users.validator);
    await openExpedient(page);

    await page.getByRole('button', { name: 'Iniciar validación' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Confirmar' }).click();
    await expect(page.getByText('En validación').first()).toBeVisible();

    await page.getByRole('button', { name: 'Enviar a aprobación' }).click();
    await page.getByRole('dialog').getByRole('button', { name: 'Confirmar' }).click();
    await expect(page.getByText('Pendiente de aprobación').first()).toBeVisible();
  });

  test('rechazar exige una observación', async ({ page }) => {
    await login(page, users.approver);
    await openExpedient(page);
    await page.getByRole('button', { name: 'Rechazar', exact: true }).click();
    await expect(page.getByRole('dialog').getByRole('button', { name: 'Confirmar' })).toBeDisabled();
    await page.getByRole('dialog').getByRole('button', { name: 'Cancelar' }).click();
  });

  test('el aprobador aprueba y queda el historial', async ({ page }) => {
    await login(page, users.approver);
    await page.getByRole('link', { name: 'Aprobaciones' }).click();
    await page.getByTestId('expedient-row').filter({ hasText: title }).click();

    await page.getByRole('button', { name: 'Aprobar' }).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByLabel(/Observación/).fill('Documentación completa.');
    await dialog.getByRole('button', { name: 'Confirmar' }).click();

    await expect(page.getByText('Aprobado').first()).toBeVisible();
    await page.getByRole('tab', { name: /Historial/ }).click();
    await expect(page.getByText('Documentación completa.')).toBeVisible();
    await page.getByRole('tab', { name: /Aprobaciones/ }).click();
    await expect(page.getByRole('main').getByText('Andrés Aprobador')).toBeVisible();
    await expect(page.getByRole('main').getByText('Aprobado', { exact: true }).first()).toBeVisible();
    await logout(page);
  });
});
