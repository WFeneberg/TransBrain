import { Routes } from '@angular/router';
import { requireAuthentication, requireCapability } from './auth/capability.guard';

const loadVehicleList = () =>
    import('./vehicles/vehicle-list.component').then((m) => m.VehicleListComponent);
const loadVehicleForm = () =>
    import('./vehicles/vehicle-form.component').then((m) => m.VehicleFormComponent);
const loadVehicleCatalog = () =>
    import('./vehicles/vehicle-catalog.component').then((m) => m.VehicleCatalogComponent);
const loadDriverList = () =>
    import('./drivers/driver-list.component').then((m) => m.DriverListComponent);
const loadDriverForm = () =>
    import('./drivers/driver-form.component').then((m) => m.DriverFormComponent);
const loadCustomerList = () =>
    import('./customers/customer-list.component').then((m) => m.CustomerListComponent);
const loadCustomerForm = () =>
    import('./customers/customer-form.component').then((m) => m.CustomerFormComponent);
const loadCustomerLocationForm = () =>
    import('./customers/customer-location-form.component').then(
        (m) => m.CustomerLocationFormComponent,
    );
const loadOrderList = () =>
    import('./orders/order-list.component').then((m) => m.OrderListComponent);
const loadOrderForm = () =>
    import('./orders/order-form.component').then((m) => m.OrderFormComponent);
const loadTourList = () => import('./tours/tour-list.component').then((m) => m.TourListComponent);
const loadTourForm = () => import('./tours/tour-form.component').then((m) => m.TourFormComponent);
const loadTourDetail = () =>
    import('./tours/tour-detail.component').then((m) => m.TourDetailComponent);
const loadInvoiceList = () =>
    import('./invoices/invoice-list.component').then((m) => m.InvoiceListComponent);
const loadInvoiceForm = () =>
    import('./invoices/invoice-form.component').then((m) => m.InvoiceFormComponent);
const loadInvoiceDetail = () =>
    import('./invoices/invoice-detail.component').then((m) => m.InvoiceDetailComponent);
const loadHome = () => import('./home/home.component').then((m) => m.HomeComponent);
const loadGeodataList = () =>
    import('./geodata/geodata-list.component').then((m) => m.GeodataListComponent);
const loadMaintenance = () =>
    import('./maintenance/maintenance.component').then((m) => m.MaintenanceComponent);
const loadMaintenanceShell = () =>
    import('./maintenance/maintenance-shell.component').then((m) => m.MaintenanceShellComponent);
const loadImportWizard = () =>
    import('./imports/import-wizard.component').then((m) => m.ImportWizardComponent);
const loadImportHistory = () =>
    import('./imports/import-history.component').then((m) => m.ImportHistoryComponent);
const loadExportPage = () =>
    import('./exports/export.component').then((m) => m.ExportPageComponent);

export const routes: Routes = [
    // angular-auth-oidc-client's checkAuth() detects the OIDC callback by comparing the
    // current URL against the configured redirectUrl (the origin, i.e. path '/'). A
    // `redirectTo` here would move the browser off '/' before checkAuth() runs, and the library
    // would then discard a valid authorization code because the path no longer matches. Home is
    // a real component mounted at '', so the callback is processed where the library expects it.
    { path: '', loadComponent: loadHome },
    { path: 'vehicles', loadComponent: loadVehicleList, canActivate: [requireAuthentication] },
    // 'new' must be registered before ':id' - the router matches path segments in order, and
    // a ':id' route registered first would swallow '/vehicles/new' by treating "new" as an id.
    {
        path: 'vehicles/new',
        loadComponent: loadVehicleForm,
        canActivate: [requireCapability('masterData.write')],
    },
    {
        path: 'vehicles/:id',
        loadComponent: loadVehicleForm,
        canActivate: [requireCapability('masterData.write')],
    },
    // Readable by every signed-in role like the vehicle list; the write controls inside are gated
    // on masterData.write individually.
    {
        path: 'vehicle-catalog',
        loadComponent: loadVehicleCatalog,
        canActivate: [requireAuthentication],
    },
    {
        path: 'customer-catalog',
        loadComponent: loadVehicleCatalog,
        canActivate: [requireAuthentication],
        data: { catalogScope: 'customer' },
    },
    {
        path: 'driver-catalog',
        loadComponent: loadVehicleCatalog,
        canActivate: [requireAuthentication],
        data: { catalogScope: 'driver' },
    },
    {
        path: 'order-catalog',
        loadComponent: loadVehicleCatalog,
        canActivate: [requireAuthentication],
        data: { catalogScope: 'order' },
    },
    { path: 'drivers', loadComponent: loadDriverList, canActivate: [requireAuthentication] },
    {
        path: 'drivers/new',
        loadComponent: loadDriverForm,
        canActivate: [requireCapability('masterData.write')],
    },
    {
        path: 'drivers/:id',
        loadComponent: loadDriverForm,
        canActivate: [requireCapability('masterData.write')],
    },
    { path: 'customers', loadComponent: loadCustomerList, canActivate: [requireAuthentication] },
    {
        path: 'customers/new',
        loadComponent: loadCustomerForm,
        canActivate: [requireCapability('masterData.write')],
    },
    {
        path: 'customers/:id',
        loadComponent: loadCustomerForm,
        canActivate: [requireCapability('masterData.write')],
    },
    {
        path: 'customers/:id/locations/new',
        loadComponent: loadCustomerLocationForm,
        canActivate: [requireCapability('masterData.write')],
    },
    {
        path: 'customers/:id/locations/:locationId',
        loadComponent: loadCustomerLocationForm,
        canActivate: [requireCapability('masterData.write')],
    },
    { path: 'orders', loadComponent: loadOrderList, canActivate: [requireAuthentication] },
    {
        path: 'orders/new',
        loadComponent: loadOrderForm,
        canActivate: [requireCapability('dispatch.write')],
    },
    {
        path: 'orders/:id',
        loadComponent: loadOrderForm,
        canActivate: [requireCapability('dispatch.write')],
    },
    { path: 'tours', loadComponent: loadTourList, canActivate: [requireAuthentication] },
    {
        path: 'tours/new',
        loadComponent: loadTourForm,
        canActivate: [requireCapability('dispatch.write')],
    },
    // Not guarded by a capability: the fahrer must reach this to start their tour, and a viewer
    // may look. The start/complete/assign buttons inside are gated individually.
    { path: 'tours/:id', loadComponent: loadTourDetail, canActivate: [requireAuthentication] },
    { path: 'invoices', loadComponent: loadInvoiceList, canActivate: [requireAuthentication] },
    {
        path: 'invoices/new',
        loadComponent: loadInvoiceForm,
        canActivate: [requireCapability('dispatch.write')],
    },
    // Readable by every signed-in role; the issue/pay/cancel buttons inside are gated on
    // dispatch.write individually.
    {
        path: 'invoices/:id',
        loadComponent: loadInvoiceDetail,
        canActivate: [requireAuthentication],
    },
    // Readable by every signed-in role; the write controls inside are gated on masterData.write.
    { path: 'geodata', loadComponent: loadGeodataList, canActivate: [requireAuthentication] },
    // The global Wartung area is system-admin territory: the shell asks which tenant to work
    // on, then offers backup/restore, the import wizard, its history and the data export — all
    // acting on the chosen tenant through the cross-tenant /api/system endpoints.
    {
        path: 'maintenance',
        loadComponent: loadMaintenanceShell,
        canActivate: [requireCapability('system.maintenance')],
        children: [
            {
                path: '',
                pathMatch: 'full',
                loadComponent: loadMaintenance,
                canActivate: [requireCapability('system.maintenance')],
            },
            {
                path: 'import',
                loadComponent: loadImportWizard,
                canActivate: [requireCapability('system.maintenance')],
            },
            {
                path: 'import/verlauf',
                loadComponent: loadImportHistory,
                canActivate: [requireCapability('system.maintenance')],
            },
            {
                path: 'export',
                loadComponent: loadExportPage,
                canActivate: [requireCapability('system.maintenance')],
            },
        ],
    },
    // The import pages lived at the top level before they moved into the Wartung area; keep old
    // bookmarks working. Order matters: 'import/verlauf' must match before 'import'.
    { path: 'import/verlauf', redirectTo: '/maintenance/import/verlauf' },
    { path: 'import', redirectTo: '/maintenance/import' },
];
