import { expect, test } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import { selectSystemTenant, signIn } from './login';

/**
 * The Export tab of the Wartung area: pick the Kunden data set, keep the CSV default, and
 * download. The file is the mirror image of the import - German column labels in the header -
 * so re-importing it through the Import tab maps every column automatically.
 */
test('systemUser_exportingCustomersAsCsv_downloadsAFileWithGermanHeaders', async ({ page }) => {
    await signIn(page, 'system');
    await page.getByTestId('nav-maintenance').click();
    await selectSystemTenant(page);
    await page.getByTestId('maintenance-tab-export').click();
    await expect(page.getByRole('heading', { name: 'Data export' })).toBeVisible();

    // Targets load from the API; Kunden is the first data set and arrives preselected. Its
    // select label carries the live row count, so only the entity name is asserted.
    await expect(page.getByTestId('export-target-select')).toContainText('Kunden');

    const downloadPromise = page.waitForEvent('download');
    await page.getByTestId('export-download').click();
    const download = await downloadPromise;

    expect(download.suggestedFilename()).toMatch(/^transbrain-export-kunden-\d{8}-\d{6}\.csv$/);
    const content = await readFile(await download.path(), 'utf-8');
    expect(content).toContain('Name;Straße;PLZ;Ort;Land;Ansprechpartner;E-Mail;Telefon;UID-Nummer;Zahlungsziel (Tage)');
});

test('viewerUser_cannotReachTheExport', async ({ page }) => {
    // The export sits in the global Wartung area and needs the system-admin role: no nav entry
    // for a viewer, and typing the URL bounces back to the home page.
    await signIn(page, 'viewer');
    await expect(page.getByTestId('nav-maintenance')).toBeHidden();
    await page.goto('/maintenance/export');
    await expect(page.getByTestId('home-greeting')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Data export' })).toBeHidden();
});

test('adminUser_cannotReachTheExport', async ({ page }) => {
    // Backup, import and export moved from the tenant admin to the global system area: a plain
    // admin no longer has the nav entry, and a typed URL bounces back to the home page.
    await signIn(page, 'admin');
    await expect(page.getByTestId('nav-maintenance')).toBeHidden();
    await page.goto('/maintenance/export');
    await expect(page.getByTestId('home-greeting')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Data export' })).toBeHidden();
});
