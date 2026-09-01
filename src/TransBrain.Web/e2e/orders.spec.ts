import { expect, test, type Page } from '@playwright/test';
import { signIn } from './login';

// dispo.user, not admin.user: orders are dispatch data, and DispatchWrite admits a dispatcher.
// Logging in as the role that will actually use this screen proves the policy choice from the
// frontend's side, the way OrderEndpointsTests proves it from the API's.
async function signInAsDispatcher(page: Page): Promise<void> {
    // The OIDC redirectUrl is always the origin ('/'), regardless of the page login was
    // initiated from (see app.routes.ts) - so login must be established from '/' first, and
    // only then can the test navigate to '/orders'. signIn() does exactly that and waits for
    // the home page it lands on.
    await signIn(page, 'dispo');
}

async function fillAddress(page: Page, party: 'consignor' | 'consignee', name: string): Promise<void> {
    await page.getByTestId(`order-${party}-name`).fill(name);
    await page.getByTestId(`order-${party}-street`).fill('Hauptstr. 1');
    await page.getByTestId(`order-${party}-postalCode`).fill('80331');
    await page.getByTestId(`order-${party}-city`).fill('München');
    await page.getByTestId(`order-${party}-country`).fill('DE');
}

test('dispatcher_createEditAndCancelOrder_throughTheUi', async ({ page }) => {
    await signInAsDispatcher(page);

    await page.goto('/orders');
    await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible();

    const consignor = `E2E${Date.now().toString(36).toUpperCase()}`;

    // Create through the UI.
    await page.getByTestId('order-add').click();
    await expect(page.getByRole('heading', { name: 'New order' })).toBeVisible();
    await fillAddress(page, 'consignor', consignor);
    await fillAddress(page, 'consignee', 'Empfaenger AG');
    await page.getByTestId('order-cargoDescription').fill('Palettenware');
    await page.getByTestId('order-cargoWeightKg').fill('12000');
    // A decimal, deliberately: step="any" is what lets a native number input accept it.
    await page.getByTestId('order-cargoLoadMeters').fill('8.4');
    await page.getByTestId('order-pickupFrom').fill('2027-03-01T08:00');
    await page.getByTestId('order-pickupTo').fill('2027-03-01T10:00');
    await page.getByTestId('order-deliveryFrom').fill('2027-03-01T12:00');
    await page.getByTestId('order-deliveryTo').fill('2027-03-01T16:00');
    await page.getByTestId('order-save').click();

    // Back on the list, the new order is visible with a generated number and Draft status.
    await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible();
    const row = page.locator('tr').filter({ hasText: consignor });
    await expect(row).toBeVisible();
    await expect(row.getByTestId('order-number')).toHaveText(/^TB-\d{4}-\d{5,}$/);
    await expect(row.getByTestId('order-status')).toHaveText('Draft');

    // Edit through the UI: change the cargo and confirm the list reflects it.
    await row.getByTestId('order-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit order' })).toBeVisible();
    await expect(page.getByTestId('order-cargoDescription')).toHaveValue('Palettenware');
    await page.getByTestId('order-cargoDescription').fill('Kuehlware');
    await page.getByTestId('order-save').click();

    await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible();
    const editedRow = page.locator('tr').filter({ hasText: consignor });
    await expect(editedRow.getByTestId('order-cargo')).toHaveText('Kuehlware');

    // Cancel through the UI. Cancelling is not deleting: the row must stay in the list with
    // its status changed to Cancelled, not disappear from it.
    await editedRow.getByTestId('order-cancel').click();
    await editedRow.getByTestId('order-cancel-confirm').click();

    const cancelledRow = page.locator('tr').filter({ hasText: consignor });
    await expect(cancelledRow).toBeVisible();
    await expect(cancelledRow.getByTestId('order-status')).toHaveText('Cancelled');

    // A cancelled order is no longer a draft, so the API refuses an edit with a 409 and the
    // message must reach the user rather than failing silently.
    await cancelledRow.getByTestId('order-edit').click();
    await expect(page.getByRole('heading', { name: 'Edit order' })).toBeVisible();
    await page.getByTestId('order-cargoDescription').fill('Zu spaet');
    await page.getByTestId('order-save').click();
    await expect(page.getByTestId('order-form-error')).toBeVisible();
    await expect(page.getByTestId('order-form-error')).toHaveText(/409/);
});

test('cancelledOrders_remainVisibleUnderTheStatusFilter', async ({ page }) => {
    await signInAsDispatcher(page);

    await page.goto('/orders');
    await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible();

    const consignor = `E2EF${Date.now().toString(36).toUpperCase()}`;

    await page.getByTestId('order-add').click();
    await fillAddress(page, 'consignor', consignor);
    await fillAddress(page, 'consignee', 'Empfaenger AG');
    await page.getByTestId('order-cargoDescription').fill('Filterware');
    await page.getByTestId('order-cargoWeightKg').fill('5000');
    await page.getByTestId('order-cargoLoadMeters').fill('3.5');
    await page.getByTestId('order-pickupFrom').fill('2027-05-01T08:00');
    await page.getByTestId('order-pickupTo').fill('2027-05-01T10:00');
    await page.getByTestId('order-deliveryFrom').fill('2027-05-01T12:00');
    await page.getByTestId('order-deliveryTo').fill('2027-05-01T16:00');
    await page.getByTestId('order-save').click();

    await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible();
    const row = page.locator('tr').filter({ hasText: consignor });
    await row.getByTestId('order-cancel').click();
    await row.getByTestId('order-cancel-confirm').click();
    await expect(page.locator('tr').filter({ hasText: consignor }).getByTestId('order-status')).toHaveText(
        'Cancelled',
    );

    // Filtering to Cancelled keeps it; filtering to Draft must not.
    await page.getByTestId('order-status-filter').click();
    await page.getByRole('option', { name: 'Cancelled', exact: true }).click();
    await expect(page.locator('tr').filter({ hasText: consignor })).toBeVisible();

    await page.getByTestId('order-status-filter').click();
    await page.getByRole('option', { name: 'Draft', exact: true }).click();
    await expect(page.locator('tr').filter({ hasText: consignor })).toHaveCount(0);
});

test('blankRequiredFields_showVisibleFieldErrorsOnSave', async ({ page }) => {
    // Proves the *rendered text*, not just that setErrors was invoked: a mat-form-field
    // renders nothing without a <mat-error> child to project, even when the control is already
    // invalid and touched via markAllAsTouched(). Removing the <mat-error> elements from
    // order-form.component.ts turns this red - it is not a test that passes regardless.
    await signInAsDispatcher(page);

    await page.goto('/orders/new');
    await expect(page.getByRole('heading', { name: 'New order' })).toBeVisible();

    // Submit an entirely blank form: client-side Validators.required blocks it before any HTTP
    // call, exercising the same rendering path a server-side `server` error would use.
    await page.getByTestId('order-save').click();

    // The invalid submit must not navigate away.
    await expect(page.getByRole('heading', { name: 'New order' })).toBeVisible();
    await expect(page.getByTestId('order-consignor-name-error')).toBeVisible();
    await expect(page.getByTestId('order-consignor-name-error')).toHaveText(/required/i);
    await expect(page.getByTestId('order-consignee-name-error')).toBeVisible();
    await expect(page.getByTestId('order-cargoDescription-error')).toBeVisible();
    await expect(page.getByTestId('order-cargoDescription-error')).toHaveText(/required/i);
});

test('directNavigationToTheForm_canStillSave', async ({ page }) => {
    // A regression test for a real defect found while capturing the guide screenshots:
    // angular-auth-oidc-client only hydrates its stored session when checkAuth() runs, which
    // the list components do and a form reached by clicking through therefore inherits. Opened
    // DIRECTLY - a bookmarked /orders/new, a reload while editing - the form had no token, and
    // saving answered "The order could not be saved. (HTTP 401)" to a plainly signed-in user.
    // Reverting OrderFormComponent's `session` pipe turns this red.
    await signInAsDispatcher(page);

    const consignor = `E2ED${Date.now().toString(36).toUpperCase()}`;

    await page.goto('/orders/new');
    await expect(page.getByRole('heading', { name: 'New order' })).toBeVisible();
    await fillAddress(page, 'consignor', consignor);
    await fillAddress(page, 'consignee', 'Empfaenger AG');
    await page.getByTestId('order-cargoDescription').fill('Direktaufruf');
    await page.getByTestId('order-cargoWeightKg').fill('7000');
    await page.getByTestId('order-cargoLoadMeters').fill('4.5');
    await page.getByTestId('order-pickupFrom').fill('2027-06-01T08:00');
    await page.getByTestId('order-pickupTo').fill('2027-06-01T10:00');
    await page.getByTestId('order-deliveryFrom').fill('2027-06-01T12:00');
    await page.getByTestId('order-deliveryTo').fill('2027-06-01T16:00');
    await page.getByTestId('order-save').click();

    await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible();
    await expect(page.locator('tr').filter({ hasText: consignor })).toBeVisible();
});

test('viewerUser_onTheOrderList_seesNoWriteActions', async ({ page }) => {
    await signIn(page, 'viewer');
    await page.goto('/orders');

    await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible();
    await expect(page.getByTestId('order-add')).toBeHidden();
});

test('disponentUser_onTheOrderList_seesTheWriteActions', async ({ page }) => {
    await signIn(page, 'dispo');
    await page.goto('/orders');

    await expect(page.getByTestId('order-add')).toBeVisible();
});
