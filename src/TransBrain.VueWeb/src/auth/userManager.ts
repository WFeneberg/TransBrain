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
    userStore: new WebStorageStateStore({ store: window.localStorage }),
});
