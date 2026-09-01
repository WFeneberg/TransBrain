import { Routes } from '@angular/router';

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
    { path: 'vehicles', loadComponent: loadVehicleList },
    // 'new' must be registered before ':id' - the router matches path segments in order, and
    // a ':id' route registered first would swallow '/vehicles/new' by treating "new" as an id.
    { path: 'vehicles/new', loadComponent: loadVehicleForm },
    { path: 'vehicles/:id', loadComponent: loadVehicleForm },
    { path: 'drivers', loadComponent: loadDriverList },
    { path: 'drivers/new', loadComponent: loadDriverForm },
    { path: 'drivers/:id', loadComponent: loadDriverForm },
    { path: 'orders', loadComponent: loadOrderList },
    { path: 'orders/new', loadComponent: loadOrderForm },
    { path: 'orders/:id', loadComponent: loadOrderForm },
    { path: 'tours', loadComponent: loadTourList },
    { path: 'tours/new', loadComponent: loadTourForm },
    { path: 'tours/:id', loadComponent: loadTourDetail },
];
