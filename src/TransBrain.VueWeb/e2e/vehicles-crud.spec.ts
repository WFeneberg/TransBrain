import { expect, test } from '@playwright/test';
import { signIn } from './login';

test('adminUser_createEditAndDeleteVehicle_throughTheUi', async ({ page }) => {
    // Signing in lands on the home page now, so the list is one navigation further on.
    await signIn(page, 'admin');
    await page.goto('/vehicles');
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
