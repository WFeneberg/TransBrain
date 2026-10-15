import { PassedInitialConfig } from 'angular-auth-oidc-client';

export const authConfig: PassedInitialConfig = {
    config: {
        authority: (window as { TRANSBRAIN_CONFIG?: { authority?: string } }).TRANSBRAIN_CONFIG?.authority ?? 'https://localhost:8080/realms/transbrain',
        redirectUrl: window.location.origin,
        postLogoutRedirectUri: window.location.origin,
        clientId: 'transbrain-spa',
        scope: 'openid profile email',
        responseType: 'code',
        silentRenew: true,
        useRefreshToken: true,
        secureRoutes: ['/api'],
    },
};
