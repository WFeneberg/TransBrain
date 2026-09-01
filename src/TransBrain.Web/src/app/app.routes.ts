import { Routes } from '@angular/router';
import { requireAuthentication, requireCapability } from './auth/capability.guard';

const loadVehicleList = () => import('./vehicles/vehicle-list.component').then((m) => m.VehicleListComponent);
const loadVehicleForm = () => import('./vehicles/vehicle-form.component').then((m) => m.VehicleFormComponent);
const loadDriverList = () => import('./drivers/driver-list.component').then((m) => m.DriverListComponent);
const loadDriverForm = () => import('./drivers/driver-form.component').then((m) => m.DriverFormComponent);
const loadOrderList = () => import('./orders/order-list.component').then((m) => m.OrderListComponent);
const loadOrderForm = () => import('./orders/order-form.component').then((m) => m.OrderFormComponent);
const loadTourList = () => import('./tours/tour-list.component').then((m) => m.TourListComponent);
const loadTourForm = () => import('./tours/tour-form.component').then((m) => m.TourFormComponent);
const loadTourDetail = () => import('./tours/tour-detail.component').then((m) => m.TourDetailComponent);
const loadHome = () => import('./home/home.component').then((m) => m.HomeComponent);

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
    { path: 'vehicles/new', loadComponent: loadVehicleForm, canActivate: [requireCapability('masterData.write')] },
    { path: 'vehicles/:id', loadComponent: loadVehicleForm, canActivate: [requireCapability('masterData.write')] },
    { path: 'drivers', loadComponent: loadDriverList, canActivate: [requireAuthentication] },
    { path: 'drivers/new', loadComponent: loadDriverForm, canActivate: [requireCapability('masterData.write')] },
    { path: 'drivers/:id', loadComponent: loadDriverForm, canActivate: [requireCapability('masterData.write')] },
    { path: 'orders', loadComponent: loadOrderList, canActivate: [requireAuthentication] },
    { path: 'orders/new', loadComponent: loadOrderForm, canActivate: [requireCapability('dispatch.write')] },
    { path: 'orders/:id', loadComponent: loadOrderForm, canActivate: [requireCapability('dispatch.write')] },
    { path: 'tours', loadComponent: loadTourList, canActivate: [requireAuthentication] },
    { path: 'tours/new', loadComponent: loadTourForm, canActivate: [requireCapability('dispatch.write')] },
    // Not guarded by a capability: the fahrer must reach this to start their tour, and a viewer
    // may look. The start/complete/assign buttons inside are gated individually.
    { path: 'tours/:id', loadComponent: loadTourDetail, canActivate: [requireAuthentication] },
];
