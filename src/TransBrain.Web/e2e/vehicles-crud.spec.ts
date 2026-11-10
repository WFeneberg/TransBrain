import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';

// A licence plate unique per run - the plate column carries a unique index, so a fixed
// literal would collide and 409 on any second run against a database that isn't reset
// between local runs (the Aspire-managed Postgres container is fresh per stack start,
// but not necessarily per test invocation within one stack's lifetime).
function uniquePlate(): string {
    return `E2E${Date.now().toString(36).toUpperCase()}`;
}

async function openVehicleList(page: Page): Promise<void> {
    await page.goto('/vehicles');
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
}

test('adminUser_createEditAndDeleteVehicle_throughTheUi', async ({ page }) => {
    // Signing in lands on the home page now, so the list is one navigation further on.
    await signIn(page, 'admin');
    await openVehicleList(page);

    const plate = uniquePlate();
    const chassisNumber = `YV2RT40A0KB${Date.now().toString().slice(-6)}`;

    // Create through the UI, including the extended master data.
    await page.getByTestId('vehicle-add').click();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
    await page.getByTestId('vehicle-licensePlate').fill(plate);
    await page.getByTestId('vehicle-isLicensePlateConfidential').getByRole('checkbox').check();
    await page.getByTestId('vehicle-type').click();
    await page.getByRole('option', { name: 'Tractor' }).click();
    await page.getByTestId('vehicle-vehicleClass').click();
    await page.getByRole('option', { name: 'Tank', exact: true }).click();
    await page.getByTestId('vehicle-make').fill('Volvo');
    await page.getByTestId('vehicle-model').fill('FH 16');
    await page.getByTestId('vehicle-manufacturedOn').fill('2021-04');
    await page.getByTestId('vehicle-powerKw').fill('368');
    await page.getByTestId('vehicle-chassisNumber').fill(chassisNumber);
    await page.getByTestId('vehicle-nextInspectionDue').fill('2027-01-01');
    await page.getByTestId('vehicle-lengthMm').fill('6100');
    await page.getByTestId('vehicle-widthMm').fill('2550');
    await page.getByTestId('vehicle-heightMm').fill('3900');
    await page.getByTestId('vehicle-loadMeters').fill('13.6');
    await page.getByTestId('vehicle-payloadKg').fill('10000');
    await page.getByTestId('vehicle-curbWeightKg').fill('8000');
    await page.getByTestId('vehicle-grossWeightKg').fill('18000');
    await page.getByTestId('vehicle-maxFifthWheelLoadKg').fill('12000');
    await page.getByTestId('vehicle-notes').fill('Created by the e2e suite.');
    await page.getByTestId('vehicle-save').click();

    // Back on the list, the new vehicle is visible with its make/model, class and the
    // confidential-plate marker.
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    const row = page.locator('tr').filter({ hasText: plate });
    await expect(row).toBeVisible();
    await expect(row).toContainText('Volvo FH 16');
    await expect(row).toContainText('Tank');
    await expect(row.getByTestId('vehicle-confidential')).toBeVisible();
    await expect(row.getByTestId('vehicle-status-chip')).toContainText('Available');

    // Edit through the UI: every extended field survived the round trip.
    await row.getByTestId('vehicle-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit vehicle' })).toBeVisible();
    await expect(page.getByTestId('vehicle-licensePlate')).toHaveValue(plate);
    await expect(
        page.getByTestId('vehicle-isLicensePlateConfidential').getByRole('checkbox'),
    ).toBeChecked();
    await expect(page.getByTestId('vehicle-vehicleClass')).toContainText('Tank');
    await expect(page.getByTestId('vehicle-make')).toHaveValue('Volvo');
    await expect(page.getByTestId('vehicle-model')).toHaveValue('FH 16');
    await expect(page.getByTestId('vehicle-manufacturedOn')).toHaveValue('2021-04');
    await expect(page.getByTestId('vehicle-powerKw')).toHaveValue('368');
    await expect(page.getByTestId('vehicle-chassisNumber')).toHaveValue(chassisNumber);
    await expect(page.getByTestId('vehicle-lengthMm')).toHaveValue('6100');
    await expect(page.getByTestId('vehicle-widthMm')).toHaveValue('2550');
    await expect(page.getByTestId('vehicle-heightMm')).toHaveValue('3900');
    await expect(page.getByTestId('vehicle-curbWeightKg')).toHaveValue('8000');
    await expect(page.getByTestId('vehicle-grossWeightKg')).toHaveValue('18000');
    await expect(page.getByTestId('vehicle-maxFifthWheelLoadKg')).toHaveValue('12000');
    await expect(page.getByTestId('vehicle-notes')).toHaveValue('Created by the e2e suite.');
    await expect(page.getByTestId('vehicle-modified')).toBeVisible();

    // Change the payload and confirm the list reflects it.
    await page.getByTestId('vehicle-payloadKg').fill('12000');
    await page.getByTestId('vehicle-save').click();

    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    const updatedRow = page.locator('tr').filter({ hasText: plate });
    await expect(updatedRow).toContainText('12000');

    // Delete asks first: cancelling keeps the row.
    await updatedRow.getByTestId('vehicle-delete').click();
    await page.getByTestId('vehicle-delete-cancel').click();
    await expect(page.getByTestId('vehicle-delete-cancel')).toBeHidden();
    await expect(updatedRow).toBeVisible();

    // Confirming deletes it: the row disappears from the list.
    await updatedRow.getByTestId('vehicle-delete').click();
    await page.getByTestId('vehicle-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: plate })).toHaveCount(0);
});

test('adminUser_changesVehicleStatus_workshopReturnAndDecommission', async ({ page }) => {
    await signIn(page, 'admin');
    await openVehicleList(page);

    const plate = uniquePlate();
    await page.getByTestId('vehicle-add').click();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
    await page.getByTestId('vehicle-licensePlate').fill(plate);
    await page.getByTestId('vehicle-payloadKg').fill('18000');
    await page.getByTestId('vehicle-loadMeters').fill('13.6');
    await page.getByTestId('vehicle-nextInspectionDue').fill('2029-03-31');
    await page.getByTestId('vehicle-save').click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();

    // The search narrows the list down to the new vehicle.
    await page.getByTestId('vehicle-search').fill(plate);
    await expect(page.getByTestId('vehicle-plate')).toHaveCount(1);
    const row = page.locator('tr').filter({ hasText: plate });
    await row.getByTestId('vehicle-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit vehicle' })).toBeVisible();
    await expect(page.getByTestId('vehicle-status')).toContainText('Available');

    // Available -> InWorkshop: sent immediately, chip and buttons follow.
    await page.getByTestId('vehicle-status-workshop').click();
    await expect(page.getByTestId('vehicle-status')).toContainText('In workshop');
    await expect(page.getByTestId('vehicle-status-workshop')).toBeHidden();
    await expect(page.getByTestId('vehicle-modified')).toBeVisible();

    // InWorkshop -> Available.
    await page.getByTestId('vehicle-status-available').click();
    await expect(page.getByTestId('vehicle-status')).toContainText('Available');
    await expect(page.getByTestId('vehicle-status-workshop')).toBeVisible();

    // Decommission asks first: cancelling changes nothing.
    await page.getByTestId('vehicle-status-decommission').click();
    await page.getByTestId('vehicle-decommission-cancel').click();
    await expect(page.getByTestId('vehicle-status')).toContainText('Available');

    // Confirming decommissions for good: no status buttons are left, only the hint.
    await page.getByTestId('vehicle-status-decommission').click();
    await page.getByTestId('vehicle-decommission-confirm').click();
    await expect(page.getByTestId('vehicle-status')).toContainText('Decommissioned');
    await expect(page.getByTestId('vehicle-status-workshop')).toHaveCount(0);
    await expect(page.getByTestId('vehicle-status-available')).toHaveCount(0);
    await expect(page.getByTestId('vehicle-status-decommission')).toHaveCount(0);
    await expect(page.getByTestId('vehicle-status-hint')).toBeVisible();
    await expect(page.getByTestId('vehicle-form-error')).toBeHidden();

    // The list shows the new status, and the status filter finds it.
    await page.getByTestId('vehicle-cancel').click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await page.getByTestId('vehicle-status-filter').click();
    await page.getByRole('option', { name: 'Decommissioned' }).click();
    await page.getByTestId('vehicle-search').fill(plate);
    const listRow = page.locator('tr').filter({ hasText: plate });
    await expect(listRow.getByTestId('vehicle-status-chip')).toContainText('Decommissioned');

    // Filtering for another status hides it and shows the empty state.
    await page.getByTestId('vehicle-status-filter').click();
    await page.getByRole('option', { name: 'Available' }).click();
    await expect(page.locator('tr').filter({ hasText: plate })).toHaveCount(0);
    await expect(page.getByTestId('vehicle-empty')).toBeVisible();
});
