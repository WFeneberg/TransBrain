import { Routes } from '@angular/router';

const loadVehicleList = () => import('./vehicles/vehicle-list.component').then((m) => m.VehicleListComponent);

export const routes: Routes = [
    // angular-auth-oidc-client's checkAuth() detects the OIDC callback by comparing the
    // current URL against the configured redirectUrl (the origin, i.e. path '/'). It must
    // therefore run on a component mounted at '' directly - a `redirectTo: 'vehicles'` here
    // would move the browser to '/vehicles' before checkAuth() runs, and the library would
    // then discard a valid authorization code because the path no longer matches. '/vehicles'
    // is kept reachable as a second route pointing at the same component, not the primary one.
    { path: '', loadComponent: loadVehicleList },
    { path: 'vehicles', loadComponent: loadVehicleList },
];
