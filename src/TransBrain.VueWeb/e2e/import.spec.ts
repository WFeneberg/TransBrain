import { expect, test } from '@playwright/test';
import { signIn } from './login';
import { chooseOption } from './select';

test('adminUser_importsCustomersFromCsv_throughTheWizard', async ({ page }) => {
    await signIn(page, 'admin');

    await page.getByTestId('nav-import').click();
    await expect(page.getByRole('heading', { name: 'Data import' })).toBeVisible();

    // Step 1: upload a small CSV. The "Land" column is deliberately missing so the mapping step
    // must be finished with a fixed value - that exercises BOTH the header auto-mapping and the
    // constant-value path. The names carry a timestamp because the data and the session history
    // survive across runs against the same AppHost instance.
    const stamp = Date.now().toString(36).toUpperCase();
    const customerName = `E2E Import ${stamp}`;
    const fileName = `kunden-${stamp}.csv`;
    const csv = ['Name;Straße;PLZ;Ort', `${customerName};Teststraße 1;86150;Augsburg`].join('\n');
    await page.getByTestId('import-file-input').setInputFiles({
        name: fileName,
        mimeType: 'text/csv',
        buffer: Buffer.from(csv, 'utf-8'),
    });
    await page.getByTestId('import-analyze').click();

    // Step 2: the CSV's single table is preselected and its sample rows are rendered; only the
    // target entity needs picking. The entity labels come from the API and stay German in every
    // UI locale, hence 'Kunden' in an otherwise English-asserting suite (see login.ts).
    await expect(page.getByTestId('import-sample-table')).toContainText('Augsburg');
    await chooseOption(page, 'import-target-select', 'Kunden');
    await page.getByTestId('import-step2-continue').click();

    // Step 3: Name/Straße/PLZ/Ort are auto-mapped by their headers; the required 'Land' is not,
    // which must block the step until the fixed value fills it.
    await expect(page.getByTestId('import-mapping-missing')).toContainText('Land');
    await expect(page.getByTestId('import-step3-continue')).toBeDisabled();
    await page.getByTestId('import-const-country').fill('DE');
    await expect(page.getByTestId('import-mapping-missing')).toBeHidden();
    await page.getByTestId('import-step3-continue').click();

    // Step 4: the preview counts the row as valid and shows it.
    await expect(page.getByTestId('import-preview-total')).toHaveText('1 rows in total');
    await expect(page.getByTestId('import-preview-valid')).toHaveText('1 valid');
    await expect(page.getByTestId('import-preview-invalid')).toHaveText('0 invalid');
    await expect(page.getByTestId('import-preview-table')).toContainText(customerName);

    // Step 5: execute and read the result.
    await page.getByTestId('import-execute').click();
    await expect(page.getByTestId('import-result-status')).toHaveText('Completed');
    await expect(page.getByTestId('import-result-imported')).toHaveText('1 imported');
    await expect(page.getByTestId('import-result-failed')).toHaveText('0 failed');

    // The import runs through the regular create commands, so the customer is real master data
    // and must show up on the customer list like a manually entered one.
    await page.goto('/customers');
    await expect(page.locator('tr').filter({ hasText: customerName })).toBeVisible();

    // History: the finished session appears with its status, and deleting it removes only the
    // session entry - cancelling first proves the confirmation actually guards the delete.
    await page.goto('/import/verlauf');
    await expect(page.getByRole('heading', { name: 'Import history' })).toBeVisible();
    const row = page.locator('tr').filter({ hasText: fileName });
    await expect(row.getByTestId('import-history-status')).toHaveText('Completed');

    await row.getByTestId('import-history-delete').click();
    await page.getByTestId('import-history-delete-cancel').click();
    await expect(page.locator('tr').filter({ hasText: fileName })).toHaveCount(1);

    await row.getByTestId('import-history-delete').click();
    await page.getByTestId('import-history-delete-confirm').click();
    await expect(page.locator('tr').filter({ hasText: fileName })).toHaveCount(0);
});

test('viewerUser_cannotReachTheImportWizard', async ({ page }) => {
    await signIn(page, 'viewer');

    // No nav entry without masterData.write, and typing the URL is caught by the router guard,
    // which bounces the viewer back to the home page.
    await expect(page.getByTestId('nav-import')).toBeHidden();
    await page.goto('/import');
    await expect(page.getByTestId('home-greeting')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Data import' })).toBeHidden();
});
