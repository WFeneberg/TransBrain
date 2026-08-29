import { expect, test } from '@playwright/test';

test('adminUser_createEditAndDeleteDriver_throughTheUi', async ({ page }) => {
    // The OIDC redirectUrl is always the origin ('/'), regardless of the page login was
    // initiated from (see app.routes.ts's comment on why '' mounts the vehicle list
    // directly) - so login must be established from '/' first, exactly as vehicles.spec.ts
    // does, and only then can the test navigate to '/drivers'. A page.goto there is a plain
    // navigation, not a fresh OIDC callback, so checkAuth() re-establishes the session
    // silently from the stored tokens rather than bouncing back to Keycloak.
    await page.goto('/');
    await page.getByTestId('login').click();
    // Keycloak's default theme also renders a "Show password" toggle button whose
    // aria-label contains the substring "password", so `getByLabel('Password')` matches
    // both it and the real input under Playwright's default case-insensitive substring
    // match and throws a strict-mode violation. Target the two form fields by their
    // stable Keycloak-theme ids instead of by label text.
    await page.locator('#username').fill('admin.user');
    await page.locator('#password').fill('admin');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();

    await page.goto('/drivers');
    await expect(page.getByRole('heading', { name: 'Drivers' })).toBeVisible();

    const lastName = `E2E${Date.now().toString(36).toUpperCase()}`;

    // Create through the UI.
    await page.getByTestId('driver-add').click();
    await expect(page.getByRole('heading', { name: 'New driver' })).toBeVisible();
    await page.getByTestId('driver-firstName').fill('Frank');
    await page.getByTestId('driver-lastName').fill(lastName);
    await page.getByTestId('driver-licenseClass-C').click();
    await page.getByTestId('driver-licenseValidUntil').fill('2028-06-30');
    await page.getByTestId('driver-save').click();

    // Back on the list, the new driver is visible.
    await expect(page.getByRole('heading', { name: 'Drivers' })).toBeVisible();
    const row = page.locator('tr').filter({ hasText: lastName });
    await expect(row).toBeVisible();

    // Edit through the UI: change the first name and confirm the list reflects it.
    await row.getByTestId('driver-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit driver' })).toBeVisible();
    await expect(page.getByTestId('driver-firstName')).toHaveValue('Frank');
    await page.getByTestId('driver-firstName').fill('Franz');
    await page.getByTestId('driver-save').click();

    await expect(page.getByRole('heading', { name: 'Drivers' })).toBeVisible();
    const updatedRow = page.locator('tr').filter({ hasText: lastName });
    await expect(updatedRow.getByTestId('driver-firstname')).toHaveText('Franz');

    // Delete through the UI: the row disappears from the list.
    await updatedRow.getByTestId('driver-delete').click();
    await expect(page.locator('tr').filter({ hasText: lastName })).toHaveCount(0);
});
