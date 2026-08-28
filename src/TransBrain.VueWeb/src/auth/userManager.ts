import { UserManager, WebStorageStateStore } from 'oidc-client-ts';

export const userManager = new UserManager({
    authority: 'https://localhost:8080/realms/transbrain',
    client_id: 'transbrain-spa',
    redirect_uri: `${window.location.origin}/callback`,
    post_logout_redirect_uri: window.location.origin,
    response_type: 'code',
    scope: 'openid profile email',
    userStore: new WebStorageStateStore({ store: window.localStorage }),
});
