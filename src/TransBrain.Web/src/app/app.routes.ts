import { Routes } from '@angular/router';

const loadVehicleList = () => import('./vehicles/vehicle-list.component').then((m) => m.VehicleListComponent);
const loadVehicleForm = () => import('./vehicles/vehicle-form.component').then((m) => m.VehicleFormComponent);
const loadDriverList = () => import('./drivers/driver-list.component').then((m) => m.DriverListComponent);
const loadDriverForm = () => import('./drivers/driver-form.component').then((m) => m.DriverFormComponent);

export const routes: Routes = [
    // angular-auth-oidc-client's checkAuth() detects the OIDC callback by comparing the
    // current URL against the configured redirectUrl (the origin, i.e. path '/'). It must
    // therefore run on a component mounted at '' directly - a `redirectTo: 'vehicles'` here
    // would move the browser to '/vehicles' before checkAuth() runs, and the library would
    // then discard a valid authorization code because the path no longer matches. '/vehicles'
    // is kept reachable as a second route pointing at the same component, not the primary one.
    { path: '', loadComponent: loadVehicleList },
    // Two canonical URLs for one screen is a stopgap, not a design choice: once real routing
    // exists (more screens, navigation), consolidate this to a single path.
    { path: 'vehicles', loadComponent: loadVehicleList },
    // 'new' must be registered before ':id' - the router matches path segments in order, and
    // a ':id' route registered first would swallow '/vehicles/new' by treating "new" as an id.
    { path: 'vehicles/new', loadComponent: loadVehicleForm },
    { path: 'vehicles/:id', loadComponent: loadVehicleForm },
    { path: 'drivers', loadComponent: loadDriverList },
    { path: 'drivers/new', loadComponent: loadDriverForm },
    { path: 'drivers/:id', loadComponent: loadDriverForm },
];
