import { authInterceptor as oidcAuthInterceptor } from 'angular-auth-oidc-client';

// Re-exported so the rest of the app depends on a local module rather than reaching into
// the library directly; `provideHttpClient(withInterceptors([authInterceptor()]))` attaches
// the bearer token to any request whose URL matches `secureRoutes` in auth.config.ts.
export const authInterceptor = oidcAuthInterceptor;
