import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';
import { chooseOption } from './select';

const CODE_ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789';

// Codes carry a unique index per kind and plates one across vehicles, so every run brings its own.
function randomSuffix(length: number): string {
    return Array.from({ length }, () => CODE_ALPHABET[Math.floor(Math.random() * CODE_ALPHABET.length)]).join('');
}

function uniquePlate(): string {
    return `E2E-CM${randomSuffix(6)}`;
}

async function fillRequiredFields(page: Page, plate: string): Promise<void> {
    await page.getByTestId('vehicle-licensePlate').fill(plate);
    await page.getByTestId('vehicle-type').click();
    await page.getByRole('option', { name: 'Tractor', exact: true }).click();
    await page.getByTestId('vehicle-payloadKg').fill('10000');
    await page.getByTestId('vehicle-loadMeters').fill('13.6');
    await page.getByTestId('vehicle-nextInspectionDue').fill('2027-01-01');
}

async function createVehicle(page: Page, plate: string): Promise<void> {
    await page.goto('/vehicles/new');
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
    await fillRequiredFields(page, plate);
    await page.getByTestId('vehicle-save').click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await expect(page.locator('tr').filter({ hasText: plate })).toBeVisible();
}

async function openForEdit(page: Page, plate: string): Promise<void> {
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await page.locator('tr').filter({ hasText: plate }).getByTestId('vehicle-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit vehicle' })).toBeVisible();
    await expect(page.getByTestId('vehicle-licensePlate')).toHaveValue(plate);
}

async function deleteVehicle(page: Page, plate: string): Promise<void> {
    await page.goto('/vehicles');
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await page.locator('tr').filter({ hasText: plate }).getByTestId('vehicle-delete').click();
    await page.getByTestId('vehicle-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: plate })).toHaveCount(0);
}

async function openInsuranceCompanies(page: Page): Promise<void> {
    await page.goto('/vehicle-catalog');
    await expect(page.getByRole('heading', { name: 'Vehicle master data' })).toBeVisible();
    await page.getByTestId('vehicle-catalog-tab-InsuranceCompany').click();
}

test('adminUser_recordsCommercialDataInsuranceAndDataSheet_andKeepsItAfterReload', async ({ page }) => {
    await signIn(page, 'admin');

    const code = `E2E${randomSuffix(6)}`;
    const insurer = `E2E Versicherung ${code}`;
    const dataSheet = 'Motor:  D13K\n  Leistung: 368 kW';

    await openInsuranceCompanies(page);
    const dialog = page.getByTestId('catalog-dialog');
    await page.getByTestId('catalog-add').click();
    await expect(dialog).toBeVisible();
    await page.getByTestId('catalog-code').fill(code);
    await page.getByTestId('catalog-name').fill(insurer);
    await page.getByTestId('catalog-save').click();
    await expect(dialog).toBeHidden();
    const entryRow = page.getByTestId('catalog-row').filter({ hasText: code });
    await expect(entryRow).toBeVisible();

    const plate = uniquePlate();
    await page.goto('/vehicles/new');
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
    await fillRequiredFields(page, plate);

    const commercial = page.getByTestId('vehicle-commercial');
    await expect(commercial).toBeVisible();
    await commercial.getByTestId('vehicle-purchasePrice').fill('125000.50');
    await commercial.getByTestId('vehicle-purchasedOn').fill('2023-03-15');
    await commercial.getByTestId('vehicle-purchaseOdometerKm').fill('12000');
    await commercial.getByTestId('vehicle-firstRegisteredOn').fill('2022-11-30');
    await commercial.getByTestId('vehicle-usefulLifeYears').fill('8');
    await commercial.getByTestId('vehicle-usefulLifeKm').fill('900000');
    await commercial.getByTestId('vehicle-costCenter').fill('KST-4711');

    await expect(page.getByTestId('vehicle-insurance')).toBeVisible();
    await chooseOption(page, 'vehicle-vehicleInsurer', insurer);
    await page.getByTestId('vehicle-vehicleInsurancePolicyNumbers').fill('POL-1\nPOL-2');
    await chooseOption(page, 'vehicle-transportInsurer', insurer);
    await page.getByTestId('vehicle-transportInsurancePolicyNumbers').fill('TR-9');

    await expect(page.getByTestId('vehicle-dataSheet-card')).toBeVisible();
    await page.getByTestId('vehicle-dataSheet').fill(dataSheet);
    await page.getByTestId('vehicle-save').click();

    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
    await expect(page.locator('tr').filter({ hasText: plate })).toBeVisible();

    await openForEdit(page, plate);
    await expect(page.getByTestId('vehicle-purchasePrice')).toHaveValue('125000.5');
    await expect(page.getByTestId('vehicle-purchasedOn')).toHaveValue('2023-03-15');
    await expect(page.getByTestId('vehicle-purchaseOdometerKm')).toHaveValue('12000');
    await expect(page.getByTestId('vehicle-firstRegisteredOn')).toHaveValue('2022-11-30');
    await expect(page.getByTestId('vehicle-usefulLifeYears')).toHaveValue('8');
    await expect(page.getByTestId('vehicle-usefulLifeKm')).toHaveValue('900000');
    await expect(page.getByTestId('vehicle-costCenter')).toHaveValue('KST-4711');
    await expect(page.getByTestId('vehicle-vehicleInsurer')).toContainText(insurer);
    await expect(page.getByTestId('vehicle-vehicleInsurancePolicyNumbers')).toHaveValue('POL-1\nPOL-2');
    await expect(page.getByTestId('vehicle-transportInsurer')).toContainText(insurer);
    await expect(page.getByTestId('vehicle-transportInsurancePolicyNumbers')).toHaveValue('TR-9');
    await expect(page.getByTestId('vehicle-dataSheet')).toHaveValue(dataSheet);

    await chooseOption(page, 'vehicle-transportInsurer', '–');
    await page.getByTestId('vehicle-costCenter').fill('KST-1');
    await page.getByTestId('vehicle-save').click();

    await openForEdit(page, plate);
    await expect(page.getByTestId('vehicle-transportInsurer')).not.toContainText(insurer);
    await expect(page.getByTestId('vehicle-vehicleInsurer')).toContainText(insurer);
    await expect(page.getByTestId('vehicle-costCenter')).toHaveValue('KST-1');
    await page.getByTestId('vehicle-cancel').click();

    await deleteVehicle(page, plate);

    await openInsuranceCompanies(page);
    await expect(entryRow.getByTestId('catalog-entry-usage')).toHaveText('0 vehicles');
    await entryRow.getByTestId('catalog-delete').click();
    await page.getByTestId('catalog-delete-confirm').click();
    await expect(entryRow).toHaveCount(0);
});

test('adminUser_maintainsSpecialDocuments_onAnExistingVehicle', async ({ page }) => {
    await signIn(page, 'admin');
    await page.goto('/vehicles/new');
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
    await expect(page.getByTestId('vehicle-documents-hint')).toBeVisible();
    await expect(page.getByTestId('vehicle-document-add')).toHaveCount(0);

    const plate = uniquePlate();
    await createVehicle(page, plate);
    await openForEdit(page, plate);

    const card = page.getByTestId('vehicle-documents');
    const dialog = page.getByTestId('document-dialog');
    const rows = card.getByTestId('vehicle-document-row');
    await expect(card.getByTestId('vehicle-documents-empty')).toBeVisible();

    await card.getByTestId('vehicle-document-add').click();
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText('Add special document');
    await chooseOption(page, 'document-type', 'ADR-Zulassungsbescheinigung');
    await page.getByTestId('document-lastCheckedOn').fill('2024-01-31');
    await page.getByTestId('document-validUntil').fill('2025-01-31');
    await page.getByTestId('document-remark').fill('Erstprüfung');
    await page.getByTestId('document-save').click();
    await expect(dialog).toBeHidden();

    await expect(rows).toHaveCount(1);
    await expect(rows.getByTestId('vehicle-document-type')).toHaveText('ADR-Zulassungsbescheinigung');
    await expect(rows.getByTestId('vehicle-document-status')).toHaveText('Expired');
    await expect(rows.getByTestId('vehicle-document-remark')).toHaveText('Erstprüfung');
    await expect(rows.getByTestId('vehicle-document-modified')).toBeVisible();

    await rows.getByTestId('vehicle-document-edit').click();
    await expect(dialog).toContainText('Edit special document');
    await expect(page.getByTestId('document-validUntil')).toHaveValue('2025-01-31');
    await page.getByTestId('document-validUntil').fill('2030-01-31');
    await page.getByTestId('document-save').click();
    await expect(dialog).toBeHidden();
    await expect(rows.getByTestId('vehicle-document-status')).toHaveText('Valid');

    await card.getByTestId('vehicle-document-add').click();
    await expect(dialog).toBeVisible();
    await chooseOption(page, 'document-type', 'ADR-Zulassungsbescheinigung');
    await page.getByTestId('document-save').click();
    await expect(page.getByTestId('document-dialog-error')).toBeVisible();
    await expect(page.getByTestId('document-dialog-error')).toContainText('409');
    await page.getByTestId('document-cancel').click();
    await expect(dialog).toBeHidden();
    await expect(rows).toHaveCount(1);

    await rows.getByTestId('vehicle-document-delete').click();
    await page.getByTestId('vehicle-document-delete-confirm').click();
    await expect(rows).toHaveCount(0);
    await expect(card.getByTestId('vehicle-documents-empty')).toBeVisible();

    await page.getByTestId('vehicle-cancel').click();
    await deleteVehicle(page, plate);
});

test('invalidCommercialValue_showsTheFieldError', async ({ page }) => {
    await signIn(page, 'admin');
    await page.goto('/vehicles/new');
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();

    await fillRequiredFields(page, uniquePlate());
    await page.getByTestId('vehicle-purchasePrice').fill('-1');
    await page.getByTestId('vehicle-usefulLifeYears').fill('0');
    await page.getByTestId('vehicle-save').click();

    await expect(page.getByTestId('vehicle-purchasePrice-error')).toBeVisible();
    await expect(page.getByTestId('vehicle-usefulLifeYears-error')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
});
