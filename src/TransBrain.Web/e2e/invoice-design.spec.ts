import { expect, test } from '@playwright/test';
import { signIn } from './login';

/**
 * The Rechnungsdesign page: the tenant's own company data and letterhead for every invoice
 * document. Saving and previewing need MasterDataWrite, so the page is an admin-only editor -
 * the same gate as the export page.
 */
test('adminUser_editingTheDesignAndPreviewing_rendersAPdfAndSaves', async ({ page }) => {
    await signIn(page, 'admin');

    const design = page.waitForResponse((response) =>
        response.url().includes('/api/invoice-design'),
    );
    await page.getByTestId('nav-invoice-design').click();
    await expect(page.getByRole('heading', { name: 'Invoice design' })).toBeVisible();
    await design;

    // The form always arrives filled: an unsaved tenant still gets the deployment defaults.
    await expect(page.getByTestId('design-companyName')).not.toHaveValue('');

    const company = `Designwerk ${Date.now().toString(36).toUpperCase().slice(-5)} GmbH`;
    await page.getByTestId('design-companyName').fill(company);

    // Preview renders the current, unsaved entries into a sample PDF shown inline.
    const preview = page.waitForResponse((response) =>
        response.url().includes('/api/invoice-design/preview'),
    );
    await page.getByTestId('design-preview').click();
    expect((await preview).status()).toBe(200);
    await expect(page.getByTestId('design-preview-frame')).toBeVisible();

    // Saving persists the design and the page confirms it; the defaults hint is gone for good.
    const save = page.waitForResponse(
        (response) =>
            response.url().includes('/api/invoice-design') && response.request().method() === 'PUT',
    );
    await page.getByTestId('design-save').click();
    expect((await save).status()).toBe(200);
    await expect(page.getByTestId('design-success')).toBeVisible();
    await expect(page.getByTestId('design-defaults-hint')).toBeHidden();
    await expect(page.getByTestId('design-updatedAt')).toBeVisible();

    // The saved value survives a reload.
    await page.reload();
    await expect(page.getByTestId('design-companyName')).toHaveValue(company);
});

test('viewerUser_navigatingToInvoiceDesign_isRedirectedHome', async ({ page }) => {
    // Design writes need masterData.write: a viewer has no nav entry, and a typed URL bounces
    // back to the home page.
    await signIn(page, 'viewer');
    await expect(page.getByTestId('nav-invoice-design')).toBeHidden();
    await page.goto('/invoice-design');
    await expect(page.getByTestId('home-greeting')).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Invoice design' })).toBeHidden();
});
