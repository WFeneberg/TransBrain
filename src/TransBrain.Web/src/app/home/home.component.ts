import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { RouterLink } from '@angular/router';
import { Area, Capability } from '../auth/capabilities';
import { SessionService } from '../auth/session.service';

interface AreaTile {
    area: Area;
    title: string;
    description: string;
    route: string;
    addLabel: string;
    addRoute: string;
    addCapability: Capability;
}

const TILES: readonly AreaTile[] = [
    {
        area: 'vehicles',
        title: 'Vehicles',
        description: 'Fleet master data: plates, payload, inspections.',
        route: '/vehicles',
        addLabel: 'Add vehicle',
        addRoute: '/vehicles/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'drivers',
        title: 'Drivers',
        description: 'Driver master data: licences, availability.',
        route: '/drivers',
        addLabel: 'Add driver',
        addRoute: '/drivers/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'orders',
        title: 'Orders',
        description: 'Transport orders from pickup to delivery.',
        route: '/orders',
        addLabel: 'New order',
        addRoute: '/orders/new',
        addCapability: 'dispatch.write',
    },
    {
        area: 'tours',
        title: 'Tours',
        description: 'Plan orders onto vehicles and follow execution.',
        route: '/tours',
        addLabel: 'Plan tour',
        addRoute: '/tours/new',
        addCapability: 'dispatch.write',
    },
];

@Component({
    selector: 'app-home',
    standalone: true,
    imports: [MatButtonModule, MatCardModule, MatChipsModule, RouterLink],
    template: `
        @if (session.isAuthenticated()) {
            <h1 data-testid="home-greeting">Welcome, {{ session.displayName() }}</h1>
            <mat-chip-set>
                @for (role of session.roles(); track role) {
                    <mat-chip data-testid="home-role-chip">{{ role }}</mat-chip>
                }
            </mat-chip-set>

            <section class="tiles">
                @for (tile of tiles; track tile.area) {
                    @if (session.areas().has(tile.area)) {
                        <mat-card [attr.data-testid]="'home-tile-' + tile.area">
                            <mat-card-title>{{ tile.title }}</mat-card-title>
                            <mat-card-content>{{ tile.description }}</mat-card-content>
                            <mat-card-actions>
                                <a mat-button [routerLink]="tile.route">Open</a>
                                @if (session.can(tile.addCapability)) {
                                    <a
                                        mat-raised-button
                                        [routerLink]="tile.addRoute"
                                        [attr.data-testid]="'home-tile-' + tile.area + '-add'"
                                        >{{ tile.addLabel }}</a
                                    >
                                }
                            </mat-card-actions>
                        </mat-card>
                    }
                }
            </section>
        } @else {
            @if (session.error(); as message) {
                <p data-testid="home-error">{{ message }}</p>
            }
            <button mat-raised-button data-testid="login" (click)="session.login()">Sign in</button>
        }
    `,
    styles: `
        .tiles {
            display: flex;
            flex-wrap: wrap;
            gap: 1rem;
        }

        .tiles mat-card {
            flex: 1 1 16rem;
        }
    `,
})
export class HomeComponent {
    protected readonly session = inject(SessionService);
    protected readonly tiles = TILES;
}
