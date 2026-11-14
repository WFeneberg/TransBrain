import { expect, test } from '@playwright/test';
import { signIn } from './login';

/**
 * Walks the import wizard end to end: upload a small CSV, map its columns onto the customer
 * target, preview, execute, and find the finished session in the history afterwards. The CSV
 * headers deliberately use the German field labels of the customer target so the wizard's
 * case-insensitive auto-mapping assigns every column without manual clicks.
 */
test('adminUser_importingACustomerCsv_walksTheWizardToACompletedImport', async ({ page }) => {
    await signIn(page, 'admin');
    // The import lives inside the Wartung (maintenance) area since it moved off the top nav.
    await page.getByTestId('nav-maintenance').click();
    await page.getByTestId('maintenance-tab-import').click();
    await expect(page.getByRole('heading', { name: 'Data import' })).toBeVisible();

    // Schritt 1: Quelle - upload a CSV with two customers. Unique names per run, because the
    // customer create command rejects duplicates and the database survives within one run.
    const stamp = Date.now().toString(36).toUpperCase();
    const csv = [
        'Name;Straße;PLZ;Ort;Land',
        `E2E Import ${stamp} GmbH;Hafenstraße 1;20095;Hamburg;DE`,
        `E2E Import ${stamp} AG;Ringstraße 2;1010;Wien;AT`,
    ].join('\n');
    await page.getByTestId('import-file-input').setInputFiles({
        name: `kunden-e2e-${stamp.toLowerCase()}.csv`,
        mimeType: 'text/csv',
        buffer: Buffer.from(csv, 'utf-8'),
    });
    await expect(page.getByTestId('import-file-name')).toContainText('kunden-e2e');
    await page.getByTestId('import-analyze').click();

    // Schritt 2: Tabelle & Ziel - a CSV yields exactly one table, which the wizard preselects;
    // only the target entity needs choosing. Target labels come from the API and stay German.
    await expect(page.getByTestId('import-table-select')).toBeVisible();
    await expect(page.getByTestId('import-sample-table')).toContainText(`E2E Import ${stamp} GmbH`);
    await page.getByTestId('import-target-select').click();
    await page.getByRole('option', { name: 'Kunden', exact: true }).click();
    await page.getByTestId('import-to-mapping').click();

    // Schritt 3: Zuordnung - the five required columns were auto-mapped by label; prove one of
    // them rather than all five, then continue.
    await expect(page.getByTestId('import-mapping')).toBeVisible();
    await expect(page.getByTestId('import-column-name')).toContainText('Name');
    await expect(page.getByTestId('import-column-city')).toContainText('Ort');
    await expect(page.getByTestId('import-required-missing')).toBeHidden();
    await page.getByTestId('import-to-preview').click();

    // Schritt 4: Vorschau - both rows are valid, so the import button is enabled.
    await expect(page.getByTestId('import-preview-total')).toContainText('2 rows total');
    await expect(page.getByTestId('import-preview-valid')).toContainText('2 valid');
    await expect(page.getByTestId('import-preview-invalid')).toContainText('0 invalid');
    await expect(page.getByTestId('import-preview-table')).toContainText('Hamburg');
    await expect(page.getByTestId('import-execute')).toBeEnabled();
    await page.getByTestId('import-execute').click();

    // Schritt 5: Ergebnis - the API creates both customers through the regular commands.
    await expect(page.getByTestId('import-result-status')).toContainText('Completed');
    await expect(page.getByTestId('import-result-imported')).toContainText('Imported: 2');
    await expect(page.getByTestId('import-result-failed')).toContainText('Failed: 0');

    // Verlauf - the finished session shows up with source name, status chip and counts.
    await page.getByTestId('import-result-history').click();
    await expect(page.getByRole('heading', { name: 'Import history' })).toBeVisible();
    const historyRow = page.locator('tr').filter({ hasText: `kunden-e2e-${stamp.toLowerCase()}` });
    await expect(historyRow.getByTestId('import-history-status')).toContainText('Completed');
    await expect(historyRow.getByTestId('import-history-counts')).toContainText('2 / 0');
});

test('viewerUser_navigatingToImport_isRedirectedHome', async ({ page }) => {
    // The import wizard sits in the Wartung area, which needs maintenance or masterData.write:
    // a viewer has no nav entry, and a typed URL (the old /import still redirects there) bounces
    // back to the home page.
    await signIn(page, 'viewer');
    await expect(page.getByTestId('nav-maintenance')).toBeHidden();
    await page.goto('/import');
    await expect(page.getByTestId('home-greeting')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Data import' })).toBeHidden();
});
