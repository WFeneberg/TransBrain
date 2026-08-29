import { expect, test, type Browser, type Page } from '@playwright/test';

// dispo.user: planning tours is dispatch work, and DispatchWrite admits a dispatcher.
async function signInAsDispatcher(page: Page): Promise<void> {
    await page.goto('/');
    await page.getByTestId('login').click();
    // Keycloak's theme also renders a "Show password" toggle whose aria-label contains
    // "password", so getByLabel('Password') matches two elements. Use the stable ids.
    await page.locator('#username').fill('dispo.user');
    await page.locator('#password').fill('dispo');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
}

/** A tour date far enough out, and unique per run, that the double-booking indexes cannot bite. */
function uniqueTourDate(offsetDays = 0): string {
    const base = new Date(Date.UTC(2090, 0, 1));
    base.setUTCDate(base.getUTCDate() + (Date.now() % 3000) + offsetDays);
    return base.toISOString().slice(0, 10);
}

// Vehicles and drivers are MASTER DATA: only an admin may create them, while planning a tour
// is a dispatcher's job. That split is the product's real shape, so the setup runs in its own
// admin browser context and the tour itself is planned by dispo.user in the test's own page.
async function asAdmin(browser: Browser, work: (page: Page) => Promise<void>): Promise<void> {
    const context = await browser.newContext();
    const page = await context.newPage();
    try {
        await page.goto('/');
        await page.getByTestId('login').click();
        await page.locator('#username').fill('admin.user');
        await page.locator('#password').fill('admin');
        await page.getByRole('button', { name: 'Sign In' }).click();
        await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
        await work(page);
    } finally {
        await context.close();
    }
}

async function createVehicle(page: Page, plate: string): Promise<void> {
    await page.goto('/vehicles/new');
    await expect(page.getByRole('heading', { name: 'New vehicle' })).toBeVisible();
    await page.getByTestId('vehicle-licensePlate').fill(plate);
    await page.getByTestId('vehicle-payloadKg').fill('18000');
    await page.getByTestId('vehicle-loadMeters').fill('13.6');
    await page.getByTestId('vehicle-nextInspectionDue').fill('2029-03-31');
    await page.getByTestId('vehicle-save').click();
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();
}

async function createDriver(page: Page, lastName: string): Promise<void> {
    await page.goto('/drivers/new');
    await expect(page.getByRole('heading', { name: 'New driver' })).toBeVisible();
    await page.getByTestId('driver-firstName').fill('Frank');
    await page.getByTestId('driver-lastName').fill(lastName);
    await page.getByTestId('driver-licenseClass-CE').click();
    await page.getByTestId('driver-licenseValidUntil').fill('2099-06-30');
    await page.getByTestId('driver-save').click();
    await expect(page.getByRole('heading', { name: 'Drivers' })).toBeVisible();
}

async function createOrder(page: Page, consignor: string, weightKg = '5000'): Promise<void> {
    await page.goto('/orders/new');
    await expect(page.getByRole('heading', { name: 'New order' })).toBeVisible();
    for (const party of ['consignor', 'consignee'] as const) {
        await page.getByTestId(`order-${party}-name`).fill(party === 'consignor' ? consignor : 'Empfaenger AG');
        await page.getByTestId(`order-${party}-street`).fill('Hauptstr. 1');
        await page.getByTestId(`order-${party}-postalCode`).fill('80331');
        await page.getByTestId(`order-${party}-city`).fill('Muenchen');
        await page.getByTestId(`order-${party}-country`).fill('DE');
    }
    await page.getByTestId('order-cargoDescription').fill('Palettenware');
    await page.getByTestId('order-cargoWeightKg').fill(weightKg);
    await page.getByTestId('order-cargoLoadMeters').fill('4.0');
    await page.getByTestId('order-pickupFrom').fill('2027-03-01T08:00');
    await page.getByTestId('order-pickupTo').fill('2027-03-01T10:00');
    await page.getByTestId('order-deliveryFrom').fill('2027-03-01T12:00');
    await page.getByTestId('order-deliveryTo').fill('2027-03-01T16:00');
    await page.getByTestId('order-save').click();
    await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible();
}

async function createTour(page: Page, date: string, plate: string, driverLastName: string): Promise<void> {
    await page.goto('/tours');
    await expect(page.getByRole('heading', { name: 'Tours' })).toBeVisible();
    await page.getByTestId('tour-add').click();
    await expect(page.getByRole('heading', { name: 'New tour' })).toBeVisible();
    await page.getByTestId('tour-tourDate').fill(date);
    await page.getByTestId('tour-vehicleId').click();
    await page.getByRole('option', { name: plate, exact: true }).click();
    await page.getByTestId('tour-driverId').click();
    await page.getByRole('option', { name: new RegExp(driverLastName) }).click();
    await page.getByTestId('tour-save').click();
}

test('dispatcher_planATourAssignAnOrderAndRunIt_throughTheUi', async ({ page, browser }) => {
    // Two Keycloak logins (admin for master data, dispo for planning) plus the whole
    // plan-assign-start-complete round trip does not fit in the default 30s.
    test.slow();

    const tag = Date.now().toString(36).toUpperCase().slice(-5);
    const plate = `M-TE ${tag.slice(0, 4)}`;
    const driverName = `TourFahrer${tag}`;
    const consignor = `TourAbsender${tag}`;
    const date = uniqueTourDate();

    await asAdmin(browser, async (admin) => {
        await createVehicle(admin, plate);
        await createDriver(admin, driverName);
    });

    await signInAsDispatcher(page);
    await createOrder(page, consignor);
    await createTour(page, date, plate, driverName);

    // Saving a tour lands on its detail page, because assigning orders is the next thing a
    // dispatcher does.
    await expect(page.getByTestId('tour-detail-status')).toHaveText('Planned');
    await expect(page.getByTestId('tour-detail-vehicle')).toHaveText(plate);
    await expect(page.getByTestId('tour-capacity-weight')).toHaveText('0 / 18000 kg');

    // Assign the order: two stops appear and the capacity readout moves.
    await page.getByTestId('tour-assign-select').click();
    await page.getByRole('option', { name: new RegExp(consignor) }).click();
    await page.getByTestId('tour-assign').click();

    await expect(page.getByTestId('tour-stop-type').first()).toHaveText('Pickup');
    await expect(page.getByTestId('tour-stop-type').nth(1)).toHaveText('Delivery');
    await expect(page.getByTestId('tour-capacity-weight')).toHaveText('5000 / 18000 kg');

    // Start: the tour and its order both move.
    await page.getByTestId('tour-start').click();
    await expect(page.getByTestId('tour-detail-status')).toHaveText('InProgress');

    await page.goto('/orders');
    await expect(page.locator('tr').filter({ hasText: consignor }).getByTestId('order-status'))
        .toHaveText('InTransit');

    // Complete: the order is delivered without anyone touching the order screen.
    await page.goto('/tours');
    await page.locator('tr').filter({ hasText: plate }).getByTestId('tour-open').click();
    await expect(page.getByRole('heading', { name: 'Tour', exact: true })).toBeVisible();
    await expect(page.getByTestId('tour-detail-status')).toHaveText('InProgress');
    await page.getByTestId('tour-complete').click();
    await expect(page.getByTestId('tour-detail-status')).toHaveText('Completed');

    await page.goto('/orders');
    await expect(page.locator('tr').filter({ hasText: consignor }).getByTestId('order-status'))
        .toHaveText('Delivered');
});

test('removingAnOrderFromATour_returnsItToDraft', async ({ page, browser }) => {
    test.slow();

    const tag = Date.now().toString(36).toUpperCase().slice(-5);
    const plate = `M-TR ${tag.slice(0, 4)}`;
    const driverName = `RemoveFahrer${tag}`;
    const consignor = `RemoveAbsender${tag}`;

    await asAdmin(browser, async (admin) => {
        await createVehicle(admin, plate);
        await createDriver(admin, driverName);
    });

    await signInAsDispatcher(page);
    await createOrder(page, consignor);
    await createTour(page, uniqueTourDate(1), plate, driverName);

    await page.getByTestId('tour-assign-select').click();
    await page.getByRole('option', { name: new RegExp(consignor) }).click();
    await page.getByTestId('tour-assign').click();
    await expect(page.getByTestId('tour-stop-sequence').first()).toHaveText('1');

    await page.getByTestId('tour-remove').click();
    await expect(page.getByTestId('tour-stop-sequence')).toHaveCount(0);
    await expect(page.getByTestId('tour-capacity-weight')).toHaveText('0 / 18000 kg');

    // Back to Draft, so it can be planned onto another tour - not stranded in Planned.
    await page.goto('/orders');
    await expect(page.locator('tr').filter({ hasText: consignor }).getByTestId('order-status'))
        .toHaveText('Draft');
});

test('doubleBookingAVehicle_showsTheConflict', async ({ page, browser }) => {
    test.slow();

    const tag = Date.now().toString(36).toUpperCase().slice(-5);
    const plate = `M-TD ${tag.slice(0, 4)}`;
    const first = `DoppeltEins${tag}`;
    const second = `DoppeltZwei${tag}`;
    const date = uniqueTourDate(2);

    await asAdmin(browser, async (admin) => {
        await createVehicle(admin, plate);
        await createDriver(admin, first);
        await createDriver(admin, second);
    });

    await signInAsDispatcher(page);
    await createTour(page, date, plate, first);
    await expect(page.getByTestId('tour-detail-status')).toHaveText('Planned');

    // Same lorry, same day, a different driver: the database's unique index refuses it and the
    // message must reach the dispatcher rather than failing silently.
    await createTour(page, date, plate, second);
    await expect(page.getByTestId('tour-form-error')).toBeVisible();
    await expect(page.getByTestId('tour-form-error')).toHaveText(/409/);
    await expect(page.getByRole('heading', { name: 'New tour' })).toBeVisible();
});

test('blankRequiredFields_showVisibleFieldErrorsOnSave', async ({ page }) => {
    // Proves the RENDERED text, not merely that setErrors was called: a mat-form-field renders
    // nothing without a <mat-error> child to project. Removing one turns this red.
    await signInAsDispatcher(page);

    await page.goto('/tours/new');
    await expect(page.getByRole('heading', { name: 'New tour' })).toBeVisible();

    await page.getByTestId('tour-save').click();

    await expect(page.getByRole('heading', { name: 'New tour' })).toBeVisible();
    await expect(page.getByTestId('tour-tourDate-error')).toBeVisible();
    await expect(page.getByTestId('tour-tourDate-error')).toHaveText(/required/i);
    await expect(page.getByTestId('tour-vehicleId-error')).toBeVisible();
    await expect(page.getByTestId('tour-driverId-error')).toBeVisible();
});

test('directNavigationToTheTourDetail_canStillAssign', async ({ page, browser }) => {
    test.slow();
    // The OIDC session-hydration regression the order form already carries: a bookmarked
    // /tours/{id} must still send a bearer token, not answer 401 to a signed-in dispatcher.
    const tag = Date.now().toString(36).toUpperCase().slice(-5);
    const plate = `M-TN ${tag.slice(0, 4)}`;
    const driverName = `DirektFahrer${tag}`;
    const consignor = `DirektAbsender${tag}`;

    await asAdmin(browser, async (admin) => {
        await createVehicle(admin, plate);
        await createDriver(admin, driverName);
    });

    await signInAsDispatcher(page);
    await createOrder(page, consignor);
    await createTour(page, uniqueTourDate(3), plate, driverName);

    // Wait for the save to land on the detail page before reading the URL - createTour
    // deliberately does not assert its outcome, because the double-booking test needs to stay
    // on the form.
    await expect(page.getByTestId('tour-detail-status')).toHaveText('Planned');
    const detailUrl = page.url();
    await page.goto('/');
    await expect(page.getByRole('heading', { name: 'Vehicles' })).toBeVisible();

    // Straight to the detail page, with no list component in between to hydrate the session.
    // Wait for the assignable-order request to settle before opening the picker: the page
    // renders as soon as the TOUR arrives, and clicking the select before its options exist
    // makes this test flaky rather than wrong.
    const draftOrders = page.waitForResponse(
        (response) => response.url().includes('/api/orders') && response.url().includes('status=Draft'),
    );
    await page.goto(detailUrl);
    await expect(page.getByTestId('tour-detail-status')).toHaveText('Planned');
    await draftOrders;

    await page.getByTestId('tour-assign-select').click();
    await page.getByRole('option', { name: new RegExp(consignor) }).click();
    await page.getByTestId('tour-assign').click();

    await expect(page.getByTestId('tour-stop-type').first()).toHaveText('Pickup');
    await expect(page.getByTestId('tour-action-error')).toHaveCount(0);
});
