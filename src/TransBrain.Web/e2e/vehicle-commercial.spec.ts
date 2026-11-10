import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';

const CODE_CHARS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';

function randomChars(length: number): string {
    return Array.from(
        { length },
        () => CODE_CHARS[Math.floor(Math.random() * CODE_CHARS.length)],
    ).join('');
}

// Catalog codes are unique per kind and plates carry a unique index, so both are made unique
// per run - the database is not necessarily reset between local runs against one Aspire stack.
function uniqueCode(): string {
    return `E2E${randomChars(6)}`;
}

function uniquePlate(): string {
    return `E2E-CM${randomChars(4)}`;
}

async function openVehicleList(page: Page): Promise<void> {
    await page.goto('/vehicles');
    await expect(page.getByRole('heading', { name: 'Vehicles', exact: true })).toBeVisible();
}

async function openNewVehicleForm(page: Page): Promise<void> {
    await openVehicleList(page);
    await page.getByTestId('vehicle-add').click();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
}

async function fillRequiredFields(page: Page, plate: string): Promise<void> {
    await page.getByTestId('vehicle-licensePlate').fill(plate);
    await page.getByTestId('vehicle-payloadKg').fill('18000');
    await page.getByTestId('vehicle-loadMeters').fill('13.6');
    await page.getByTestId('vehicle-nextInspectionDue').fill('2029-03-31');
}

async function openVehicleFromList(page: Page, plate: string): Promise<void> {
    await expect(page.getByRole('heading', { name: 'Vehicles', exact: true })).toBeVisible();
    await page.getByTestId('vehicle-search').fill(plate);
    const row = page.locator('tr').filter({ hasText: plate });
    await expect(row).toBeVisible();
    await row.getByTestId('vehicle-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit vehicle' })).toBeVisible();
    await expect(page.getByTestId('vehicle-licensePlate')).toHaveValue(plate);
}

async function deleteVehicleFromList(page: Page, plate: string): Promise<void> {
    await expect(page.getByRole('heading', { name: 'Vehicles', exact: true })).toBeVisible();
    await page.getByTestId('vehicle-search').fill(plate);
    const row = page.locator('tr').filter({ hasText: plate });
    await row.getByTestId('vehicle-delete').click();
    await page.getByTestId('vehicle-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: plate })).toHaveCount(0);
}

async function chooseOption(page: Page, testId: string, name: string): Promise<void> {
    await page.getByTestId(testId).click();
    await page.getByRole('option', { name, exact: true }).click();
}

test('adminUser_recordsCommercialDataInsuranceAndDataSheet_andKeepsItAfterReload', async ({
    page,
}) => {
    await signIn(page, 'admin');

    const code = uniqueCode();
    const insurer = `E2E Versicherung ${code}`;
    const plate = uniquePlate();
    const dataSheet = 'Motor:  D13K\n  Leistung: 368 kW';

    // Create the insurance company on the catalog page.
    await page.goto('/vehicle-catalog');
    await expect(page.getByRole('heading', { name: 'Vehicle master data' })).toBeVisible();
    await page.getByTestId('vehicle-catalog-tab-InsuranceCompany').click();
    await expect(page).toHaveURL(/kind=InsuranceCompany/);
    await page.getByTestId('catalog-add').click();
    await expect(page.getByTestId('catalog-dialog')).toBeVisible();
    await page.getByTestId('catalog-code').fill(code);
    await page.getByTestId('catalog-name').fill(insurer);
    await page.getByTestId('catalog-save').click();
    await expect(page.getByTestId('catalog-dialog')).toBeHidden();
    const catalogRow = page.getByTestId('catalog-row').filter({ hasText: code });
    await expect(catalogRow.getByTestId('catalog-entry-name')).toHaveText(insurer);

    // Create a vehicle with commercial data, insurance and a data sheet.
    await page.getByTestId('vehicle-catalog-back').click();
    await expect(page.getByRole('heading', { name: 'Vehicles', exact: true })).toBeVisible();
    await page.getByTestId('vehicle-add').click();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
    await fillRequiredFields(page, plate);
    await expect(page.getByTestId('vehicle-commercial')).toBeVisible();
    await expect(page.getByTestId('vehicle-insurance')).toBeVisible();
    await expect(page.getByTestId('vehicle-dataSheet-card')).toBeVisible();
    await page.getByTestId('vehicle-purchasePrice').fill('125000.50');
    await page.getByTestId('vehicle-purchasedOn').fill('2023-03-15');
    await page.getByTestId('vehicle-purchaseOdometerKm').fill('12000');
    await page.getByTestId('vehicle-firstRegisteredOn').fill('2022-11-30');
    await page.getByTestId('vehicle-usefulLifeYears').fill('8');
    await page.getByTestId('vehicle-usefulLifeKm').fill('900000');
    await page.getByTestId('vehicle-costCenter').fill('KST-4711');
    await chooseOption(page, 'vehicle-vehicleInsurer', insurer);
    await expect(page.getByTestId('vehicle-vehicleInsurer')).toContainText(insurer);
    await page.getByTestId('vehicle-vehicleInsurancePolicyNumbers').fill('POL-1\nPOL-2');
    await chooseOption(page, 'vehicle-transportInsurer', insurer);
    await expect(page.getByTestId('vehicle-transportInsurer')).toContainText(insurer);
    await page.getByTestId('vehicle-transportInsurancePolicyNumbers').fill('TR-9');
    await page.getByTestId('vehicle-dataSheet').fill(dataSheet);
    await page.getByTestId('vehicle-save').click();

    // Every value survived the round trip.
    await openVehicleFromList(page, plate);
    await expect(page.getByTestId('vehicle-purchasePrice')).toHaveValue(/^125000\.50?$/);
    await expect(page.getByTestId('vehicle-purchasedOn')).toHaveValue('2023-03-15');
    await expect(page.getByTestId('vehicle-purchaseOdometerKm')).toHaveValue('12000');
    await expect(page.getByTestId('vehicle-firstRegisteredOn')).toHaveValue('2022-11-30');
    await expect(page.getByTestId('vehicle-usefulLifeYears')).toHaveValue('8');
    await expect(page.getByTestId('vehicle-usefulLifeKm')).toHaveValue('900000');
    await expect(page.getByTestId('vehicle-costCenter')).toHaveValue('KST-4711');
    await expect(page.getByTestId('vehicle-vehicleInsurer')).toContainText(insurer);
    await expect(page.getByTestId('vehicle-vehicleInsurancePolicyNumbers')).toHaveValue(
        'POL-1\nPOL-2',
    );
    await expect(page.getByTestId('vehicle-transportInsurer')).toContainText(insurer);
    await expect(page.getByTestId('vehicle-transportInsurancePolicyNumbers')).toHaveValue('TR-9');
    await expect(page.getByTestId('vehicle-dataSheet')).toHaveValue(dataSheet);

    // Clear the transport insurer and change the cost center.
    await chooseOption(page, 'vehicle-transportInsurer', '–');
    await expect(page.getByTestId('vehicle-transportInsurer')).not.toContainText(insurer);
    await page.getByTestId('vehicle-costCenter').fill('KST-1');
    await page.getByTestId('vehicle-save').click();

    await openVehicleFromList(page, plate);
    await expect(page.getByTestId('vehicle-costCenter')).toHaveValue('KST-1');
    await expect(page.getByTestId('vehicle-vehicleInsurer')).toContainText(insurer);
    await expect(page.getByTestId('vehicle-transportInsurer')).not.toContainText(insurer);
    await expect(page.getByTestId('vehicle-transportInsurancePolicyNumbers')).toHaveValue('TR-9');
    await page.getByTestId('vehicle-cancel').click();

    // Delete the vehicle, then the now unused insurance company.
    await deleteVehicleFromList(page, plate);
    await page.getByTestId('vehicle-catalog-link').click();
    await page.getByTestId('vehicle-catalog-tab-InsuranceCompany').click();
    await expect(catalogRow.getByTestId('catalog-entry-usage')).toHaveText('0 vehicles');
    await catalogRow.getByTestId('catalog-delete').click();
    await page.getByTestId('catalog-delete-confirm').click();
    await expect(page.getByTestId('catalog-row').filter({ hasText: code })).toHaveCount(0);
    await expect(page.getByTestId('catalog-error')).toBeHidden();
});

test('adminUser_maintainsSpecialDocuments_onAnExistingVehicle', async ({ page }) => {
    await signIn(page, 'admin');
    await openNewVehicleForm(page);

    // A vehicle that does not exist yet cannot hold documents.
    await expect(page.getByTestId('vehicle-documents-hint')).toBeVisible();
    await expect(page.getByTestId('vehicle-document-add')).toHaveCount(0);

    const plate = uniquePlate();
    await fillRequiredFields(page, plate);
    await page.getByTestId('vehicle-save').click();

    await openVehicleFromList(page, plate);
    await expect(page.getByTestId('vehicle-documents-empty')).toBeVisible();

    // Add an ADR certificate that has already expired.
    await page.getByTestId('vehicle-document-add').click();
    await expect(page.getByTestId('document-dialog')).toBeVisible();
    await chooseOption(page, 'document-type', 'ADR-Zulassungsbescheinigung');
    await page.getByTestId('document-lastCheckedOn').fill('2024-01-31');
    await page.getByTestId('document-validUntil').fill('2025-01-31');
    await page.getByTestId('document-remark').fill('Erstprüfung');
    await page.getByTestId('document-save').click();
    await expect(page.getByTestId('document-dialog')).toBeHidden();

    const rows = page.getByTestId('vehicle-document-row');
    await expect(rows).toHaveCount(1);
    const row = rows.first();
    await expect(row.getByTestId('vehicle-document-type')).toHaveText(
        'ADR-Zulassungsbescheinigung',
    );
    await expect(row.getByTestId('vehicle-document-status')).toHaveText('Expired');
    await expect(row.getByTestId('vehicle-document-remark')).toHaveText('Erstprüfung');
    await expect(row.getByTestId('vehicle-document-modified')).toBeVisible();
    await expect(page.getByTestId('vehicle-documents-empty')).toHaveCount(0);

    // Renew it.
    await row.getByTestId('vehicle-document-edit').click();
    await expect(page.getByTestId('document-dialog')).toBeVisible();
    await expect(page.getByTestId('document-validUntil')).toHaveValue('2025-01-31');
    await page.getByTestId('document-validUntil').fill('2030-01-31');
    await page.getByTestId('document-save').click();
    await expect(page.getByTestId('document-dialog')).toBeHidden();
    await expect(row.getByTestId('vehicle-document-status')).toHaveText('Valid');

    // A second document of the same type is rejected.
    await page.getByTestId('vehicle-document-add').click();
    await expect(page.getByTestId('document-dialog')).toBeVisible();
    await chooseOption(page, 'document-type', 'ADR-Zulassungsbescheinigung');
    await page.getByTestId('document-save').click();
    await expect(page.getByTestId('document-dialog-error')).toBeVisible();
    await expect(page.getByTestId('document-dialog-error')).toContainText('409');
    await page.getByTestId('document-cancel').click();
    await expect(page.getByTestId('document-dialog')).toBeHidden();
    await expect(rows).toHaveCount(1);

    // Delete it after confirming.
    await row.getByTestId('vehicle-document-delete').click();
    await page.getByTestId('vehicle-document-delete-confirm').click();
    await expect(page.getByTestId('vehicle-documents-empty')).toBeVisible();
    await expect(rows).toHaveCount(0);

    await page.getByTestId('vehicle-cancel').click();
    await deleteVehicleFromList(page, plate);
});

test('invalidCommercialValue_showsTheFieldError', async ({ page }) => {
    await signIn(page, 'admin');
    await openNewVehicleForm(page);

    await fillRequiredFields(page, uniquePlate());
    await page.getByTestId('vehicle-purchasePrice').fill('-1');
    await page.getByTestId('vehicle-usefulLifeYears').fill('0');
    await page.getByTestId('vehicle-save').click();

    await expect(page.getByTestId('vehicle-purchasePrice-error')).toBeVisible();
    await expect(page.getByTestId('vehicle-usefulLifeYears-error')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
});
