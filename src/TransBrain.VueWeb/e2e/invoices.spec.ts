import { expect, test, type Browser, type Page } from '@playwright/test';
import { signIn } from './login';
import { chooseOption } from './select';

// dispo.user: billing is dispatch work, and DispatchWrite admits a dispatcher. Master data
// (vehicle, driver, customer) is created by an admin in its own context, same as tours.spec.ts.
async function signInAsDispatcher(page: Page): Promise<void> {
    await signIn(page, 'dispo');
}

/** A tour date far enough out, and unique per run, that the double-booking indexes cannot bite. */
function uniqueTourDate(): string {
    const base = new Date(Date.UTC(2090, 0, 1));
    base.setUTCDate(base.getUTCDate() + (Date.now() % 3000));
    return base.toISOString().slice(0, 10);
}

async function asAdmin(browser: Browser, work: (page: Page) => Promise<void>): Promise<void> {
    const context = await browser.newContext();
    const page = await context.newPage();
    try {
        await signIn(page, 'admin');
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

async function createCustomer(page: Page, name: string): Promise<void> {
    await page.goto('/customers/new');
    await expect(page.getByRole('heading', { name: 'New customer' })).toBeVisible();
    await page.getByTestId('customer-name').fill(name);
    await page.getByTestId('customer-street').fill('Rechnungsweg 7');
    await page.getByTestId('customer-postalCode').fill('86899');
    await page.getByTestId('customer-city').fill('Landsberg');
    await chooseOption(page, 'customer-country', 'Germany');
    await page.getByTestId('customer-save').click();
    await expect(page.getByRole('heading', { name: 'Customers' })).toBeVisible();
}

// The billable-order rows in the invoice form show order number, route, and CARGO - not the
// consignor - so the cargo description doubles as the test's unique row anchor.
async function createOrder(page: Page, consignor: string, cargo: string): Promise<void> {
    await page.goto('/orders/new');
    await expect(page.getByRole('heading', { name: 'New order' })).toBeVisible();
    for (const party of ['consignor', 'consignee'] as const) {
        await page.getByTestId(`order-${party}-name`).fill(party === 'consignor' ? consignor : 'Empfaenger AG');
        await page.getByTestId(`order-${party}-street`).fill('Hauptstr. 1');
        await page.getByTestId(`order-${party}-postalCode`).fill('80331');
        await page.getByTestId(`order-${party}-city`).fill('Muenchen');
        await chooseOption(page, `order-${party}-country`, 'Germany');
    }
    await page.getByTestId('order-cargoDescription').fill(cargo);
    await page.getByTestId('order-cargoWeightKg').fill('5000');
    await page.getByTestId('order-cargoLoadMeters').fill('4.0');
    await page.getByTestId('order-pickupFrom').fill('2027-03-01T08:00');
    await page.getByTestId('order-pickupTo').fill('2027-03-01T10:00');
    await page.getByTestId('order-deliveryFrom').fill('2027-03-01T12:00');
    await page.getByTestId('order-deliveryTo').fill('2027-03-01T16:00');
    await page.getByTestId('order-save').click();
    await expect(page.getByRole('heading', { name: 'Orders' })).toBeVisible();
}

// A plain <select> carries the whole label "TB-2027-00042 - Consignor (5000 kg)", and the test
// only knows the consignor part, so the matching option is picked by its value.
async function selectOrderByConsignor(page: Page, consignor: string): Promise<void> {
    const option = page.locator('#tour-assign-select option').filter({ hasText: consignor }).first();
    await expect(option).toBeAttached();
    const value = await option.getAttribute('value');
    await page.getByTestId('tour-assign-select').selectOption(value!);
}

/** Plans a tour over the order and runs it to Completed, which delivers the order. */
async function deliverOrder(page: Page, consignor: string, plate: string, driverLastName: string): Promise<void> {
    const draftOrders = page.waitForResponse(
        (response) => response.url().includes('/api/orders') && response.url().includes('status=Draft'),
    );
    await page.goto('/tours');
    await expect(page.getByRole('heading', { name: 'Tours' })).toBeVisible();
    await page.getByTestId('tour-add').click();
    await expect(page.getByRole('heading', { name: 'New tour' })).toBeVisible();
    await page.getByTestId('tour-tourDate').fill(uniqueTourDate());
    await chooseOption(page, 'tour-vehicleId', plate);
    await chooseOption(page, 'tour-driverId', `${driverLastName}, Frank`);
    await page.getByTestId('tour-save').click();

    await expect(page.getByTestId('tour-detail-status')).toHaveText('Planned');
    await draftOrders;
    await selectOrderByConsignor(page, consignor);
    await page.getByTestId('tour-assign').click();
    await expect(page.getByTestId('tour-stop-type').first()).toHaveText('Pickup');
    await page.getByTestId('tour-start').click();
    await expect(page.getByTestId('tour-detail-status')).toHaveText('InProgress');
    await page.getByTestId('tour-complete').click();
    await expect(page.getByTestId('tour-detail-status')).toHaveText('Completed');
}

/** Opens the invoice form and waits for the billable orders it offers to actually arrive. */
async function openInvoiceForm(page: Page): Promise<void> {
    const billable = page.waitForResponse((response) => response.url().includes('/api/invoices/billable-orders'));
    await page.goto('/invoices/new');
    await expect(page.getByRole('heading', { name: 'New invoice' })).toBeVisible();
    await billable;
}

/** Fills the form over the one order carrying `cargo` and saves; lands on the draft's detail. */
async function createDraftInvoice(page: Page, customer: string, cargo: string): Promise<void> {
    await chooseOption(page, 'invoice-customerId', customer);
    const row = page.locator('tr').filter({ hasText: cargo });
    await row.getByTestId('invoice-line-select').click();
    await row.getByTestId('invoice-line-amount').fill('950');
    await page.getByTestId('invoice-save').click();
    await expect(page.getByTestId('invoice-detail-status')).toHaveText('Draft');
}

test('dispatcher_billsADeliveredOrder_throughItsWholeLifecycle', async ({ page, browser }) => {
    // Three Keycloak logins plus the deliver-create-cancel-rebill-pay round trip: by far the
    // longest flow here, and Vite serves Vuetify unbundled in dev, so each page load costs more
    // than in the Angular suite. test.slow()'s 90s is not enough.
    test.setTimeout(240_000);

    const tag = Date.now().toString(36).toUpperCase().slice(-5);
    const plate = `M-RG ${tag.slice(0, 4)}`;
    const driverName = `RechnungsFahrer${tag}`;
    const consignor = `RechnungsAbsender${tag}`;
    const customer = `RechnungsKunde${tag}`;
    const cargo = `Rechnungsgut ${tag}`;

    await asAdmin(browser, async (admin) => {
        await createVehicle(admin, plate);
        await createDriver(admin, driverName);
        await createCustomer(admin, customer);
    });

    await signInAsDispatcher(page);
    await createOrder(page, consignor, cargo);
    await deliverOrder(page, consignor, plate, driverName);

    // Bill the delivered order: the draft's detail shows the snapshotted customer and the line.
    await openInvoiceForm(page);
    await createDraftInvoice(page, customer, cargo);
    await expect(page.getByTestId('invoice-detail-number')).toContainText(/RE-\d{4}-\d{5}/);
    await expect(page.getByTestId('invoice-detail-customer')).toContainText(customer);
    await expect(page.getByTestId('invoice-line-net').first()).toContainText('950.00');

    // While the draft exists its order is spoken for: the form no longer offers it.
    await openInvoiceForm(page);
    await expect(page.locator('tr').filter({ hasText: cargo })).toHaveCount(0);

    // Cancel the draft from the list; the confirmation is inline in the row's action cell.
    await page.goto('/invoices');
    await expect(page.getByRole('heading', { name: 'Billing' })).toBeVisible();
    const invoiceRow = page.locator('tr').filter({ hasText: customer });
    await invoiceRow.getByTestId('invoice-cancel').click();
    await invoiceRow.getByTestId('invoice-cancel-confirm').click();
    await expect(invoiceRow.getByTestId('invoice-status')).toHaveText('Cancelled');

    // The order is billable again; bill it again and walk the lifecycle to Paid.
    await openInvoiceForm(page);
    await expect(page.locator('tr').filter({ hasText: cargo })).toBeVisible();
    await createDraftInvoice(page, customer, cargo);
    await page.getByTestId('invoice-detail-issue').click();
    await expect(page.getByTestId('invoice-detail-status')).toHaveText('Issued');
    await expect(page.getByTestId('invoice-detail-dueDate')).not.toContainText('—');

    await page.getByTestId('invoice-detail-pay').click();
    await expect(page.getByTestId('invoice-detail-status')).toHaveText('Paid');
    await expect(page.getByTestId('invoice-detail-paidAt')).not.toContainText('—');
});

test('viewer_seesTheInvoiceListWithoutAnyWriteActions', async ({ page }) => {
    await signIn(page, 'viewer');

    await page.goto('/invoices');
    await expect(page.getByRole('heading', { name: 'Billing' })).toBeVisible();
    await expect(page.getByTestId('invoice-table')).toBeVisible();

    // Read is allowed, but neither the create button nor any row action is offered.
    await expect(page.getByTestId('invoice-add')).toHaveCount(0);
    await expect(page.getByTestId('invoice-issue')).toHaveCount(0);
    await expect(page.getByTestId('invoice-pay')).toHaveCount(0);
    await expect(page.getByTestId('invoice-cancel')).toHaveCount(0);
});
