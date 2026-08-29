import { expect, test } from '@playwright/test';

test('adminUser_createEditAndDeleteDriver_throughTheUi', async ({ page }) => {
    // The OIDC redirect_uri always lands on '/callback', which then replaces the URL with '/'
    // (see AuthCallback.vue) - regardless of the page login was initiated from. So login must
    // be established from '/' first, exactly as vehicles.spec.ts does, and only then can the
    // test navigate to '/drivers'. A page.goto there afterwards is a plain navigation, not a
    // fresh OIDC callback, so the session persists silently from sessionStorage rather than
    // bouncing back to Keycloak.
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

test('blankRequiredNames_showVisibleFieldErrorsOnSave', async ({ page }) => {
    // This proves the *rendered text*, not just that the field-error state was recorded
    // internally - a form that computes fieldErrors.firstName/lastName correctly but never
    // renders them (the exact defect Task 1 of this phase exists to fix on the Angular side)
    // would fail this assertion while passing a test that only checked the mechanism.
    await page.goto('/');
    await page.getByTestId('login').click();
    await page.locator('#username').fill('admin.user');
    await page.locator('#password').fill('admin');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();

    await page.goto('/drivers/new');
    await expect(page.getByRole('heading', { name: 'New driver' })).toBeVisible();

    // Leave firstName and lastName blank; fill everything else so only the two name fields
    // are invalid. Client-side required validation blocks the submit before any HTTP call -
    // this exercises the same field-error rendering path a server-mapped error would also
    // use, just driven by the client-side "required" message instead of a `server` one.
    await page.getByTestId('driver-licenseClass-C').click();
    await page.getByTestId('driver-licenseValidUntil').fill('2028-06-30');
    await page.getByTestId('driver-save').click();

    // The invalid submit must not navigate away.
    await expect(page.getByRole('heading', { name: 'New driver' })).toBeVisible();
    await expect(page.getByTestId('driver-firstName-error')).toBeVisible();
    await expect(page.getByTestId('driver-firstName-error')).toHaveText(/required/i);
    await expect(page.getByTestId('driver-lastName-error')).toBeVisible();
    await expect(page.getByTestId('driver-lastName-error')).toHaveText(/required/i);
});
