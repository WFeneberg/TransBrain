import { expect, test } from '@playwright/test';
import { signIn } from './login';

/**
 * The Rechnungsdesign page: the admin edits the tenant's company data and letterhead, renders a
 * sample invoice with the unsaved form content via Vorschau, and saves. The design arrives
 * prefilled - either the tenant's own row or the deployment defaults - so the company name is
 * never empty.
 */
test('adminUser_editingTheDesignAndPreviewing_showsTheSampleInvoiceAndSaves', async ({ page }) => {
    await signIn(page, 'admin');
    await page.getByTestId('nav-invoice-design').click();
    await expect(page.getByRole('heading', { name: 'Invoice design' })).toBeVisible();

    const companyName = page.getByTestId('design-companyName');
    await expect(companyName).not.toHaveValue('');
    await companyName.fill('Muster Spedition GmbH');
    await page.getByTestId('design-headerText').fill('Vielen Dank für Ihren Auftrag!');

    // The preview POSTs the current, unsaved design; the API answers with a PDF that the page
    // shows inline. Rendering the sample invoice server-side takes a moment.
    await page.getByTestId('design-preview').click();
    await expect(page.getByTestId('design-preview-frame')).toBeVisible({ timeout: 15_000 });

    await page.getByTestId('design-save').click();
    await expect(page.getByTestId('design-save-success')).toBeVisible();
    // A saved design is the tenant's own; the defaults hint must be gone.
    await expect(page.getByTestId('design-defaults-hint')).toBeHidden();
});

test('viewerUser_cannotReachTheInvoiceDesigner', async ({ page }) => {
    // The designer needs masterData.write like the export: no nav entry for a viewer, and
    // typing the URL bounces back to the home page.
    await signIn(page, 'viewer');
    await expect(page.getByTestId('nav-invoice-design')).toBeHidden();
    await page.goto('/invoice-design');
    await expect(page.getByTestId('home-greeting')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Invoice design' })).toBeHidden();
});
