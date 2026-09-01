import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterOutlet } from '@angular/router';
import { SessionService } from './auth/session.service';

@Component({
    imports: [RouterOutlet, RouterLink, MatToolbarModule, MatButtonModule],
    selector: 'app-root',
    styleUrl: './app.scss',
    templateUrl: './app.html',
})
export class App {
    protected readonly session = inject(SessionService);

    constructor() {
        // The one checkAuth() of the whole SPA. It must run from a component that is mounted at
        // the OIDC redirectUrl (the origin, i.e. path ''), which App always is - see the comment
        // in app.routes.ts about why moving it broke the callback once before.
        this.session.initialize();
    }
}
