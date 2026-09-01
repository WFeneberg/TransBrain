import { Injectable, computed, inject, signal } from '@angular/core';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { Observable, catchError, map, of, shareReplay, tap } from 'rxjs';
import { Area, AppRole, Capability, areasFor, capabilitiesFor, knownRoles } from './capabilities';

interface UserClaims {
    realm_access?: { roles?: unknown };
    name?: unknown;
    preferred_username?: unknown;
}

/**
 * The single place this SPA knows anything about who is signed in.
 *
 * checkAuth() runs exactly ONCE, driven by the App component, and every consumer shares that one
 * result through `ready`. Before this service existed, all nine screens called checkAuth()
 * themselves - four list components to decide whether to render at all, five forms purely to
 * hydrate the stored session before their first request went out.
 *
 * The roles come from `userData`, which angular-auth-oidc-client fills from the userinfo
 * endpoint. The realm's realm-roles-in-id-token mapper is what puts realm_access there; without
 * it userData carries no roles at all (verified against real tokens, see the realm file).
 */
@Injectable({ providedIn: 'root' })
export class SessionService {
    private readonly oidc = inject(OidcSecurityService);

    private readonly authenticated = signal(false);
    private readonly claims = signal<UserClaims | null>(null);
    private readonly checkError = signal<string | null>(null);

    readonly isAuthenticated = this.authenticated.asReadonly();
    readonly error = this.checkError.asReadonly();

    readonly roles = computed<AppRole[]>(() => {
        const raw = this.claims()?.realm_access?.roles;
        const values = Array.isArray(raw) ? raw.filter((role): role is string => typeof role === 'string') : [];
        return knownRoles(values);
    });

    readonly displayName = computed<string>(() => {
        const data = this.claims();
        if (typeof data?.name === 'string' && data.name.length > 0) {
            return data.name;
        }
        if (typeof data?.preferred_username === 'string') {
            return data.preferred_username;
        }
        return '';
    });

    readonly areas = computed<ReadonlySet<Area>>(() => areasFor(this.roles()));

    private readonly capabilities = computed<ReadonlySet<Capability>>(() => capabilitiesFor(this.roles()));

    /**
     * Emits once checkAuth() has settled. Guards and any request made before the first render
     * must wait on this, or angular-auth-oidc-client has not yet rehydrated the stored session
     * and the request goes out without a bearer token.
     *
     * shareReplay(1) is what makes "exactly once" true: later subscribers get the stored value
     * instead of triggering a second checkAuth().
     */
    readonly ready: Observable<boolean> = this.oidc.checkAuth().pipe(
        tap(({ isAuthenticated, userData }) => {
            this.authenticated.set(isAuthenticated);
            this.claims.set((userData as UserClaims | null) ?? null);
        }),
        map(({ isAuthenticated }) => isAuthenticated),
        catchError(() => {
            // A checkAuth failure (Keycloak unreachable, a rejected code) must not leave callers
            // hanging on a never-emitting observable - guards would block the router forever.
            this.checkError.set('Could not verify your sign-in status. Please try signing in again.');
            this.authenticated.set(false);
            return of(false);
        }),
        shareReplay(1),
    );

    /** Called by the App component so the one checkAuth() runs as early as the app itself. */
    initialize(): void {
        this.ready.subscribe();
    }

    can(capability: Capability): boolean {
        return this.capabilities().has(capability);
    }

    hasRole(role: AppRole): boolean {
        return this.roles().includes(role);
    }

    login(): void {
        this.oidc.authorize();
    }

    logout(): void {
        this.oidc.logoff().subscribe();
    }
}
