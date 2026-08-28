import { UserManager, WebStorageStateStore } from 'oidc-client-ts';

export const userManager = new UserManager({
    authority: 'https://localhost:8080/realms/transbrain',
    client_id: 'transbrain-spa',
    // Must match a route that calls completeLogin() (see views/AuthCallback.vue) - oidc-client-ts
    // requires signinRedirectCallback() to run on the exact redirect_uri, unlike the Angular
    // app's angular-auth-oidc-client, which processes the callback wherever checkAuth() runs.
    redirect_uri: `${window.location.origin}/callback`,
    post_logout_redirect_uri: window.location.origin,
    response_type: 'code',
    scope: 'openid profile email',
    // Aligned with the Angular app's angular-auth-oidc-client config (silentRenew /
    // useRefreshToken there, automaticSilentRenew / monitorSession here): without these, the
    // Vue session dies at Keycloak's access-token lifetime with a bare 401 and no renewal.
    automaticSilentRenew: true,
    monitorSession: true,
    // sessionStorage, not localStorage: a bearer token in localStorage survives a browser
    // restart, widening the XSS exposure window. Angular's client defaults to sessionStorage;
    // this now matches it deliberately rather than by omission.
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
});
