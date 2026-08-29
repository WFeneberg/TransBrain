import { expect, test } from '@playwright/test';

test('adminUser_createEditAndDeleteVehicle_throughTheUi', async ({ page }) => {
    // See drivers.spec.ts for why login must be established from '/' - the OIDC redirectUrl
    // is always the origin, and '/' already mounts the vehicle list directly.
    await page.goto('/');
    await page.getByTestId('login').click();
    // Keycloak's default theme also renders a "Show password" toggle button whose
    // aria-label contains the substring "password", so `getByLabel('Password')` matches
    // both it and the real input under Playwright's default case-insensitive substring
    // match and throws a strict-mode violation. Target the two form fields by their
    // stable Keycloak-theme ids instead of by label text.
    await page.locator('#username').fill('admin.user');
    await page.locator('#password').fill('admin');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();

    // A licence plate unique per run - the plate column carries a unique index, so a fixed
    // literal would collide and 409 on any second run against a database that isn't reset
    // between local runs (the Aspire-managed Postgres container is fresh per stack start,
    // but not necessarily per test invocation within one stack's lifetime).
    const plate = `E2E${Date.now().toString(36).toUpperCase()}`;

    // Create through the UI.
    await page.getByTestId('vehicle-add').click();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
    await page.getByTestId('vehicle-licensePlate').fill(plate);
    await page.getByTestId('vehicle-type').click();
    await page.getByRole('option', { name: 'Tractor' }).click();
    await page.getByTestId('vehicle-payloadKg').fill('10000');
    await page.getByTestId('vehicle-loadMeters').fill('13.6');
    await page.getByTestId('vehicle-nextInspectionDue').fill('2027-01-01');
    await page.getByTestId('vehicle-save').click();

    // Back on the list, the new vehicle is visible.
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    const row = page.locator('tr').filter({ hasText: plate });
    await expect(row).toBeVisible();

    // Edit through the UI: change the payload and confirm the list reflects it.
    await row.getByTestId('vehicle-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit vehicle' })).toBeVisible();
    await expect(page.getByTestId('vehicle-licensePlate')).toHaveValue(plate);
    await page.getByTestId('vehicle-payloadKg').fill('12000');
    await page.getByTestId('vehicle-save').click();

    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    const updatedRow = page.locator('tr').filter({ hasText: plate });
    await expect(updatedRow).toContainText('12000');

    // Delete through the UI: the row disappears from the list.
    await updatedRow.getByTestId('vehicle-delete').click();
    await expect(page.locator('tr').filter({ hasText: plate })).toHaveCount(0);
});
