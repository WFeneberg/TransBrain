import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';
import { chooseOption } from './select';

// A licence plate unique per run - the plate column carries a unique index, so a fixed
// literal would collide and 409 on any second run against a database that isn't reset
// between local runs (the Aspire-managed Postgres container is fresh per stack start,
// but not necessarily per test invocation within one stack's lifetime).
function uniquePlate(): string {
    return `E2E${Date.now().toString(36).toUpperCase()}`;
}

async function fillRequiredFields(page: Page, plate: string): Promise<void> {
    await page.getByTestId('vehicle-licensePlate').fill(plate);
    await page.getByTestId('vehicle-type').click();
    await page.getByRole('option', { name: 'Tractor' }).click();
    await page.getByTestId('vehicle-payloadKg').fill('10000');
    await page.getByTestId('vehicle-loadMeters').fill('13.6');
    await page.getByTestId('vehicle-nextInspectionDue').fill('2027-01-01');
}

test('adminUser_createEditAndDeleteVehicle_throughTheUi', async ({ page }) => {
    // Signing in lands on the home page now, so the list is one navigation further on.
    await signIn(page, 'admin');
    await page.goto('/vehicles');
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();

    const plate = uniquePlate();
    // The API upper-cases the chassis number, so an upper-case value round-trips unchanged.
    const chassisNumber = `YV2RT40A0${plate}`.slice(0, 17);

    // Create through the UI, master data of the legacy fleet screen included.
    await page.getByTestId('vehicle-add').click();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
    await fillRequiredFields(page, plate);
    await page.getByTestId('vehicle-isLicensePlateConfidential').check();
    await page.getByTestId('vehicle-make').fill('Volvo');
    await page.getByTestId('vehicle-model').fill('FH 16');
    // The class options are catalog entries ordered by name, so "Tank" may need scrolling to.
    await chooseOption(page, 'vehicle-vehicleClass', 'Tank');
    await page.getByTestId('vehicle-manufacturedOn').fill('2020-05');
    await page.getByTestId('vehicle-powerKw').fill('368');
    await page.getByTestId('vehicle-chassisNumber').fill(chassisNumber);
    await page.getByTestId('vehicle-lengthMm').fill('6000');
    await page.getByTestId('vehicle-widthMm').fill('2550');
    await page.getByTestId('vehicle-heightMm').fill('3900');
    await page.getByTestId('vehicle-curbWeightKg').fill('8000');
    await page.getByTestId('vehicle-grossWeightKg').fill('18000');
    await page.getByTestId('vehicle-maxFifthWheelLoadKg').fill('12000');
    await page.getByTestId('vehicle-notes').fill('Created by the e2e suite.');
    await page.getByTestId('vehicle-save').click();

    // Back on the list, the new vehicle is visible.
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    const row = page.locator('tr').filter({ hasText: plate });
    await expect(row).toBeVisible();
    await expect(row).toContainText('Volvo FH 16');
    await expect(row).toContainText('Tank');
    await expect(row.getByTestId('vehicle-status-chip')).toContainText('Available');

    // Edit through the UI: every new field survived the round trip, then change the payload
    // and confirm the list reflects it.
    await row.getByTestId('vehicle-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit vehicle' })).toBeVisible();
    await expect(page.getByTestId('vehicle-licensePlate')).toHaveValue(plate);
    await expect(page.getByTestId('vehicle-isLicensePlateConfidential')).toBeChecked();
    await expect(page.getByTestId('vehicle-make')).toHaveValue('Volvo');
    await expect(page.getByTestId('vehicle-model')).toHaveValue('FH 16');
    await expect(page.getByTestId('vehicle-vehicleClass')).toContainText('Tank');
    await expect(page.getByTestId('vehicle-manufacturedOn')).toHaveValue('2020-05');
    await expect(page.getByTestId('vehicle-powerKw')).toHaveValue('368');
    await expect(page.getByTestId('vehicle-chassisNumber')).toHaveValue(chassisNumber);
    await expect(page.getByTestId('vehicle-lengthMm')).toHaveValue('6000');
    await expect(page.getByTestId('vehicle-widthMm')).toHaveValue('2550');
    await expect(page.getByTestId('vehicle-heightMm')).toHaveValue('3900');
    await expect(page.getByTestId('vehicle-curbWeightKg')).toHaveValue('8000');
    await expect(page.getByTestId('vehicle-grossWeightKg')).toHaveValue('18000');
    await expect(page.getByTestId('vehicle-maxFifthWheelLoadKg')).toHaveValue('12000');
    await expect(page.getByTestId('vehicle-notes')).toHaveValue('Created by the e2e suite.');
    await expect(page.getByTestId('vehicle-modified')).toBeVisible();
    await page.getByTestId('vehicle-payloadKg').fill('12000');
    await page.getByTestId('vehicle-save').click();

    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    const updatedRow = page.locator('tr').filter({ hasText: plate });
    await expect(updatedRow).toContainText('12000');

    // Delete through the UI: cancelling the confirmation keeps the row, confirming removes it.
    await updatedRow.getByTestId('vehicle-delete').click();
    await page.getByTestId('vehicle-delete-cancel').click();
    await expect(page.locator('tr').filter({ hasText: plate })).toHaveCount(1);

    await updatedRow.getByTestId('vehicle-delete').click();
    await page.getByTestId('vehicle-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: plate })).toHaveCount(0);
});

test('adminUser_changesVehicleStatus_workshopAndBackThenDecommission', async ({ page }) => {
    await signIn(page, 'admin');
    await page.goto('/vehicles/new');
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();

    const plate = uniquePlate();
    await fillRequiredFields(page, plate);
    await page.getByTestId('vehicle-save').click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();

    await page.locator('tr').filter({ hasText: plate }).getByTestId('vehicle-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit vehicle' })).toBeVisible();
    await expect(page.getByTestId('vehicle-status')).toContainText('Available');

    // Workshop and back: each change is sent immediately, without Save.
    await page.getByTestId('vehicle-status-workshop').click();
    await expect(page.getByTestId('vehicle-status')).toContainText('In workshop');
    await expect(page.getByTestId('vehicle-status-workshop')).toBeHidden();

    await page.getByTestId('vehicle-status-available').click();
    await expect(page.getByTestId('vehicle-status')).toContainText('Available');

    // Decommissioning asks first; cancelling changes nothing.
    await page.getByTestId('vehicle-status-decommission').click();
    await page.getByTestId('vehicle-decommission-cancel').click();
    await expect(page.getByTestId('vehicle-status')).toContainText('Available');

    await page.getByTestId('vehicle-status-decommission').click();
    await page.getByTestId('vehicle-decommission-confirm').click();
    await expect(page.getByTestId('vehicle-status')).toContainText('Decommissioned');
    await expect(page.getByText('Decommissioned vehicles cannot be reactivated.')).toBeVisible();
    await expect(page.getByTestId('vehicle-status-workshop')).toBeHidden();
    await expect(page.getByTestId('vehicle-status-available')).toBeHidden();
    await expect(page.getByTestId('vehicle-status-decommission')).toBeHidden();

    await page.getByTestId('vehicle-cancel').click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await expect(page.locator('tr').filter({ hasText: plate }).getByTestId('vehicle-status-chip')).toContainText(
        'Decommissioned',
    );
});
