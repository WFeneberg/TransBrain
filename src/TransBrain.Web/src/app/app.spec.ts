import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { App } from './app';
import { SessionService } from './auth/session.service';

/**
 * A stub rather than the real service: App's constructor calls initialize(), which subscribes to
 * SessionService.ready, which runs angular-auth-oidc-client's checkAuth(). With the real service
 * wired up, creating the component would try to reach Keycloak over the network - a unit test
 * that needs a running realm is not a unit test.
 */
class SessionServiceStub {
    readonly ready = of(false);
    readonly isAuthenticated = () => false;
    readonly error = () => null;
    readonly roles = () => [];
    readonly displayName = () => '';
    readonly areas = () => new Set<string>();
    can = () => false;
    hasRole = () => false;
    login = (): void => undefined;
    logout = (): void => undefined;
    initialize = (): void => undefined;
}

describe('App', () => {
    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [App],
            providers: [provideRouter([]), { provide: SessionService, useClass: SessionServiceStub }],
        }).compileComponents();
    });

    it('should create the app', () => {
        const fixture = TestBed.createComponent(App);
        const app = fixture.componentInstance;
        expect(app).toBeTruthy();
    });
});
