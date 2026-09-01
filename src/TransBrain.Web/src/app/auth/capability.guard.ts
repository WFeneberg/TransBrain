import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { Capability } from './capabilities';
import { SessionService } from './session.service';

/**
 * Both guards wait on SessionService.ready before deciding. Without that wait the router runs
 * before checkAuth() has rehydrated the stored session, and a directly opened URL - a reload, a
 * bookmark - would be judged as signed-out and bounced to '/'.
 */
export const requireAuthentication: CanActivateFn = () => {
    const session = inject(SessionService);
    const router = inject(Router);

    return session.ready.pipe(map(() => (session.isAuthenticated() ? true : router.createUrlTree(['/']))));
};

/**
 * Guards a route behind one capability. A user who fails goes to their own home page rather than
 * to a 403 screen: they are signed in and the app has somewhere useful to put them.
 *
 * Note what is NOT guarded this way - the four list routes and /tours/:id take
 * requireAuthentication only. The API lets every role read them (Policies.Read), so a
 * client-side block would be stricter than the server for no reason. Hiding a tile means "you do
 * not need this", not "you may not have this".
 */
export function requireCapability(capability: Capability): CanActivateFn {
    return () => {
        const session = inject(SessionService);
        const router = inject(Router);

        return session.ready.pipe(
            map(() => {
                if (!session.isAuthenticated()) {
                    return router.createUrlTree(['/']);
                }
                return session.can(capability) ? true : router.createUrlTree(['/']);
            }),
        );
    };
}
