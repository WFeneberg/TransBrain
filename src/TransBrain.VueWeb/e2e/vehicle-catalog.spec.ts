import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';
import { chooseOption, expectOptionNotOffered } from './select';

// Codes carry a unique index per kind and plates one across vehicles, so every run brings its
// own - the database is not necessarily reset between local runs within one Aspire stack.
function uniqueSuffix(): string {
    return Date.now().toString(36).toUpperCase();
}

async function openCatalog(page: Page, kind: 'VehicleClass' | 'CarrierGroup'): Promise<void> {
    await page.goto('/vehicles');
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await page.getByTestId('vehicle-catalog-link').click();
    await expect(page.getByRole('heading', { name: 'Vehicle master data' })).toBeVisible();
    await expect(page.getByText('Selection lists for the vehicle form')).toBeVisible();
    await page.getByTestId(`vehicle-catalog-tab-${kind}`).click();
}

async function fillRequiredVehicleFields(page: Page, plate: string): Promise<void> {
    await page.getByTestId('vehicle-licensePlate').fill(plate);
    await page.getByTestId('vehicle-type').click();
    await page.getByRole('option', { name: 'Tractor', exact: true }).click();
    await page.getByTestId('vehicle-payloadKg').fill('10000');
    await page.getByTestId('vehicle-loadMeters').fill('13.6');
    await page.getByTestId('vehicle-nextInspectionDue').fill('2027-01-01');
}

test('adminUser_maintainsCarrierGroups_andUsesThemOnAVehicle', async ({ page }) => {
    await signIn(page, 'admin');
    await openCatalog(page, 'CarrierGroup');

    const suffix = uniqueSuffix();
    const code = `E2E${suffix}`;
    const name = `E2E carrier ${suffix}`;
    const renamed = `E2E carrier renamed ${suffix}`;
    const plate = `E2E${suffix}`;
    const dialog = page.getByTestId('catalog-dialog');

    // Add an entry: active by default, unused.
    await page.getByTestId('catalog-add').click();
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText('New entry');
    await expect(page.getByTestId('catalog-active')).toBeChecked();
    await page.getByTestId('catalog-code').fill(code);
    await page.getByTestId('catalog-name').fill(name);
    await page.getByTestId('catalog-save').click();
    await expect(dialog).toBeHidden();

    const row = page.getByTestId('catalog-row').filter({ hasText: code });
    await expect(row).toBeVisible();
    await expect(row.getByTestId('catalog-entry-code')).toHaveText(code);
    await expect(row.getByTestId('catalog-entry-name')).toHaveText(name);
    await expect(row.getByTestId('catalog-entry-status')).toHaveText('Active');
    await expect(row.getByTestId('catalog-entry-usage')).toHaveText('0 vehicles');

    // Rename it - the code is read-only once created.
    await row.getByTestId('catalog-edit').click();
    await expect(dialog).toContainText('Edit entry');
    await expect(page.getByTestId('catalog-code')).toHaveValue(code);
    await expect(page.getByTestId('catalog-code')).toBeDisabled();
    await page.getByTestId('catalog-name').fill(renamed);
    await page.getByTestId('catalog-save').click();
    await expect(dialog).toBeHidden();
    await expect(row.getByTestId('catalog-entry-name')).toHaveText(renamed);

    // Use it on a new vehicle.
    await page.getByTestId('vehicle-catalog-back').click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await page.getByTestId('vehicle-add').click();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
    await fillRequiredVehicleFields(page, plate);
    await chooseOption(page, 'vehicle-carrierGroup', renamed);
    await page.getByTestId('vehicle-save').click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();

    // The list search also matches the carrier group's name.
    await page.getByTestId('vehicle-search').fill(renamed);
    await expect(page.getByTestId('vehicle-plate')).toHaveText([plate]);
    await page.getByTestId('vehicle-search').fill('');

    // In use now: counted, and no longer deletable.
    await page.getByTestId('vehicle-catalog-link').click();
    await page.getByTestId('vehicle-catalog-tab-CarrierGroup').click();
    await expect(row.getByTestId('catalog-entry-usage')).toHaveText(/^1 vehicle/);
    await expect(row.getByTestId('catalog-delete')).toBeDisabled();

    // Deactivate it.
    await row.getByTestId('catalog-edit').click();
    // Wait for the form to bind the entry's state - unchecking a not-yet-bound box is a no-op.
    await expect(page.getByTestId('catalog-active')).toBeChecked();
    await page.getByTestId('catalog-active').uncheck();
    await expect(page.getByTestId('catalog-active')).not.toBeChecked();
    await page.getByTestId('catalog-save').click();
    await expect(dialog).toBeHidden();
    await expect(row.getByTestId('catalog-entry-status')).toHaveText('Inactive');

    // The vehicle that uses it keeps it, marked as inactive...
    await page.getByTestId('vehicle-catalog-back').click();
    await page.locator('tr').filter({ hasText: plate }).getByTestId('vehicle-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit vehicle' })).toBeVisible();
    await expect(page.getByTestId('vehicle-carrierGroup')).toContainText(`${renamed} (inactive)`);
    await page.getByTestId('vehicle-cancel').click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();

    // ...but a new vehicle is not offered it. Both catalogs load together, so a seeded class
    // being offered proves the carrier groups have arrived as well.
    await page.getByTestId('vehicle-add').click();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
    await chooseOption(page, 'vehicle-vehicleClass', 'Tank');
    await expectOptionNotOffered(page, 'vehicle-carrierGroup', renamed);
    await page.getByTestId('vehicle-cancel').click();

    // Delete the vehicle, after which the entry is free to go.
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await page.locator('tr').filter({ hasText: plate }).getByTestId('vehicle-delete').click();
    await page.getByTestId('vehicle-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: plate })).toHaveCount(0);

    await page.getByTestId('vehicle-catalog-link').click();
    await page.getByTestId('vehicle-catalog-tab-CarrierGroup').click();
    await expect(row.getByTestId('catalog-entry-usage')).toHaveText('0 vehicles');
    await expect(row.getByTestId('catalog-delete')).toBeEnabled();

    // Cancelling keeps it, confirming removes it.
    await row.getByTestId('catalog-delete').click();
    await page.getByTestId('catalog-delete-cancel').click();
    await expect(row).toHaveCount(1);
    await row.getByTestId('catalog-delete').click();
    await page.getByTestId('catalog-delete-confirm').click();
    await expect(row).toHaveCount(0);
});

test('duplicateCode_showsTheConflict', async ({ page }) => {
    await signIn(page, 'admin');
    await openCatalog(page, 'VehicleClass');
    await expect(page.getByTestId('catalog-row').filter({ hasText: 'Tank' })).not.toHaveCount(0);

    const name = `E2E duplicate ${uniqueSuffix()}`;
    await page.getByTestId('catalog-add').click();
    await page.getByTestId('catalog-code').fill('Tank');
    await page.getByTestId('catalog-name').fill(name);
    await page.getByTestId('catalog-save').click();

    await expect(page.getByTestId('catalog-dialog-error')).toContainText('409');
    await expect(page.getByTestId('catalog-dialog')).toBeVisible();

    await page.getByTestId('catalog-cancel').click();
    await expect(page.getByTestId('catalog-dialog')).toBeHidden();
    await expect(page.getByTestId('catalog-row').filter({ hasText: name })).toHaveCount(0);
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

    await page.getByTestId('vehicle-catalog-tab-CarrierGroup').click();
    await expect(page.getByTestId('catalog-add')).toHaveCount(0);
    await expect(page.getByTestId('catalog-edit')).toHaveCount(0);
    await expect(page.getByTestId('catalog-delete')).toHaveCount(0);
});
