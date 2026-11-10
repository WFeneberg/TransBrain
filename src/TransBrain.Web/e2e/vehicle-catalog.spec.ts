import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';

// Codes are unique per kind and plates carry a unique index, so both are made unique per run -
// the database is not necessarily reset between local runs against one Aspire stack.
function uniqueSuffix(): string {
    return Date.now().toString(36).toUpperCase();
}

async function openVehicleList(page: Page): Promise<void> {
    await page.goto('/vehicles');
    await expect(page.getByRole('heading', { name: 'Vehicles', exact: true })).toBeVisible();
}

async function openCatalog(page: Page, kind: 'VehicleClass' | 'CarrierGroup'): Promise<void> {
    await openVehicleList(page);
    await page.getByTestId('vehicle-catalog-link').click();
    await expect(page.getByRole('heading', { name: 'Vehicle master data' })).toBeVisible();
    await page.getByTestId(`vehicle-catalog-tab-${kind}`).click();
    await expect(page).toHaveURL(new RegExp(`kind=${kind}`));
}

test('adminUser_maintainsCarrierGroups_andUsesThemOnAVehicle', async ({ page }) => {
    await signIn(page, 'admin');
    await openCatalog(page, 'CarrierGroup');

    const suffix = uniqueSuffix();
    const code = `E2E${suffix}`;
    const name = `E2E carrier ${suffix}`;
    const renamed = `E2E carrier group ${suffix}`;
    const plate = `E2E${suffix}`;

    // Add an entry: it shows up unused and active.
    await page.getByTestId('catalog-add').click();
    await expect(page.getByTestId('catalog-dialog')).toBeVisible();
    await expect(page.getByTestId('catalog-active').getByRole('checkbox')).toBeChecked();
    await page.getByTestId('catalog-code').fill(code);
    await page.getByTestId('catalog-name').fill(name);
    await page.getByTestId('catalog-save').click();
    await expect(page.getByTestId('catalog-dialog')).toBeHidden();

    const row = page.getByTestId('catalog-row').filter({ hasText: code });
    await expect(row).toBeVisible();
    await expect(row.getByTestId('catalog-entry-code')).toHaveText(code);
    await expect(row.getByTestId('catalog-entry-name')).toHaveText(name);
    await expect(row.getByTestId('catalog-entry-status')).toHaveText('Active');
    await expect(row.getByTestId('catalog-entry-usage')).toHaveText('0 vehicles');

    // Rename it: the code is read-only once created.
    await row.getByTestId('catalog-edit').click();
    await expect(page.getByTestId('catalog-code')).toHaveValue(code);
    await expect(page.getByTestId('catalog-code')).not.toBeEditable();
    await page.getByTestId('catalog-name').fill(renamed);
    await page.getByTestId('catalog-save').click();
    await expect(page.getByTestId('catalog-dialog')).toBeHidden();
    await expect(row.getByTestId('catalog-entry-name')).toHaveText(renamed);

    // Use it on a new vehicle.
    await page.getByTestId('vehicle-catalog-back').click();
    await expect(page.getByRole('heading', { name: 'Vehicles', exact: true })).toBeVisible();
    await page.getByTestId('vehicle-add').click();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
    await page.getByTestId('vehicle-licensePlate').fill(plate);
    await page.getByTestId('vehicle-payloadKg').fill('18000');
    await page.getByTestId('vehicle-loadMeters').fill('13.6');
    await page.getByTestId('vehicle-nextInspectionDue').fill('2029-03-31');
    await page.getByTestId('vehicle-carrierGroup').click();
    await page.getByRole('option', { name: renamed, exact: true }).click();
    await expect(page.getByTestId('vehicle-carrierGroup')).toContainText(renamed);
    await page.getByTestId('vehicle-save').click();
    await expect(page.getByRole('heading', { name: 'Vehicles', exact: true })).toBeVisible();
    await expect(page.locator('tr').filter({ hasText: plate })).toBeVisible();

    // The list search also matches the carrier group's name.
    await page.getByTestId('vehicle-search').fill(renamed);
    await expect(page.getByTestId('vehicle-plate')).toHaveCount(1);
    await expect(page.getByTestId('vehicle-plate')).toHaveText(plate);

    // Back on the catalog the entry is in use, so it cannot be deleted.
    await page.getByTestId('vehicle-catalog-link').click();
    await page.getByTestId('vehicle-catalog-tab-CarrierGroup').click();
    await expect(row.getByTestId('catalog-entry-usage')).toHaveText(/^\s*1 vehicles?\s*$/);
    await expect(row.getByTestId('catalog-delete')).toBeDisabled();

    // Deactivate it instead.
    await row.getByTestId('catalog-edit').click();
    // Wait for the form to bind the entry's state - unchecking a not-yet-bound box is a no-op.
    const active = page.getByTestId('catalog-active').getByRole('checkbox');
    await expect(active).toBeChecked();
    await active.uncheck();
    await expect(active).not.toBeChecked();
    await page.getByTestId('catalog-save').click();
    await expect(page.getByTestId('catalog-dialog')).toBeHidden();
    await expect(row.getByTestId('catalog-entry-status')).toHaveText('Inactive');

    // A new vehicle can no longer choose it ...
    await page.getByTestId('vehicle-catalog-back').click();
    await page.getByTestId('vehicle-add').click();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
    await page.getByTestId('vehicle-carrierGroup').click();
    await expect(page.getByRole('option', { name: '–', exact: true })).toBeVisible();
    await expect(page.getByRole('option').filter({ hasText: renamed })).toHaveCount(0);
    await page.keyboard.press('Escape');
    await page.getByTestId('vehicle-cancel').click();

    // ... while the vehicle that already has it keeps it, marked inactive.
    await expect(page.getByRole('heading', { name: 'Vehicles', exact: true })).toBeVisible();
    const vehicleRow = page.locator('tr').filter({ hasText: plate });
    await vehicleRow.getByTestId('vehicle-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit vehicle' })).toBeVisible();
    await expect(page.getByTestId('vehicle-carrierGroup')).toContainText(`${renamed} (inactive)`);
    await page.getByTestId('vehicle-cancel').click();

    // Delete the vehicle, then the now unused entry.
    await expect(page.getByRole('heading', { name: 'Vehicles', exact: true })).toBeVisible();
    await vehicleRow.getByTestId('vehicle-delete').click();
    await page.getByTestId('vehicle-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: plate })).toHaveCount(0);

    await page.getByTestId('vehicle-catalog-link').click();
    await page.getByTestId('vehicle-catalog-tab-CarrierGroup').click();
    await expect(row.getByTestId('catalog-entry-usage')).toHaveText('0 vehicles');
    await row.getByTestId('catalog-delete').click();
    await page.getByTestId('catalog-delete-confirm').click();
    await expect(page.getByTestId('catalog-row').filter({ hasText: code })).toHaveCount(0);
    await expect(page.getByTestId('catalog-error')).toBeHidden();
});

test('duplicateCode_showsTheConflict', async ({ page }) => {
    await signIn(page, 'admin');
    await openCatalog(page, 'VehicleClass');
    await expect(page.getByTestId('catalog-row').filter({ hasText: 'Tank' }).first()).toBeVisible();

    await page.getByTestId('catalog-add').click();
    await page.getByTestId('catalog-code').fill('Tank');
    await page.getByTestId('catalog-name').fill(`Duplicate ${uniqueSuffix()}`);
    await page.getByTestId('catalog-save').click();

    await expect(page.getByTestId('catalog-dialog-error')).toContainText('409');
    await expect(page.getByTestId('catalog-dialog')).toBeVisible();
    await page.getByTestId('catalog-cancel').click();
    await expect(page.getByTestId('catalog-dialog')).toBeHidden();
});

test('viewerUser_onTheCatalog_seesNoWriteActions', async ({ page }) => {
    await signIn(page, 'viewer');
    await openCatalog(page, 'VehicleClass');

    await expect(page.getByTestId('vehicle-catalog-tab-VehicleClass')).toBeVisible();
    await expect(page.getByTestId('vehicle-catalog-tab-CarrierGroup')).toBeVisible();
    await expect(page.getByTestId('catalog-row').filter({ hasText: 'Kühler' })).toBeVisible();
    await expect(page.getByTestId('catalog-add')).toHaveCount(0);
    await expect(page.getByTestId('catalog-edit')).toHaveCount(0);
    await expect(page.getByTestId('catalog-delete')).toHaveCount(0);
});
