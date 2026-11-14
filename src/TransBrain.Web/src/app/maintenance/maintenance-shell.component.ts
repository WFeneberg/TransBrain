import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';

/**
 * Shell of the Wartung (maintenance) area: one tab bar over the admin tooling - backup/restore,
 * the import wizard and the import history. The tabs mirror the route guards: Datensicherung
 * needs the maintenance capability, both import tabs need masterData.write. In practice only
 * admins hold either, so the whole area is admin territory.
 */
@Component({
    selector: 'app-maintenance-shell',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatTabsModule, RouterLink, RouterLinkActive, RouterOutlet],
    template: `
        <div class="shell">
            <nav mat-tab-nav-bar [tabPanel]="panel">
                @if (session.can('maintenance')) {
                    <a
                        mat-tab-link
                        routerLink="/maintenance"
                        routerLinkActive
                        #backupLink="routerLinkActive"
                        [routerLinkActiveOptions]="{ exact: true }"
                        [active]="backupLink.isActive"
                        data-testid="maintenance-tab-backup"
                    >
                        {{ i18n.t('maintenance.tabs.backup') }}
                    </a>
                }
                @if (session.can('masterData.write')) {
                    <a
                        mat-tab-link
                        routerLink="/maintenance/import"
                        routerLinkActive
                        #importLink="routerLinkActive"
                        [routerLinkActiveOptions]="{ exact: true }"
                        [active]="importLink.isActive"
                        data-testid="maintenance-tab-import"
                    >
                        {{ i18n.t('maintenance.tabs.import') }}
                    </a>
                    <a
                        mat-tab-link
                        routerLink="/maintenance/import/verlauf"
                        routerLinkActive
                        #historyLink="routerLinkActive"
                        [routerLinkActiveOptions]="{ exact: true }"
                        [active]="historyLink.isActive"
                        data-testid="maintenance-tab-history"
                    >
                        {{ i18n.t('maintenance.tabs.history') }}
                    </a>
                }
            </nav>
            <mat-tab-nav-panel #panel>
                <router-outlet />
            </mat-tab-nav-panel>
        </div>
    `,
    styles: `
        .shell {
            max-width: 1200px;
            margin: 0 auto;
        }

        nav {
            margin-bottom: 24px;
        }
    `,
})
export class MaintenanceShellComponent {
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);
}
