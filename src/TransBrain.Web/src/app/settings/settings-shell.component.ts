import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';

/**
 * Shell of the Einstellungen (settings) area: the tenant-level configuration pages that used to
 * sit as separate toolbar entries. One tab per page — the invoice design editor stays admin-only
 * like its route guard, so its tab only shows with masterData.write; the geodata maintenance is
 * readable by every signed-in role, so its tab always shows.
 */
@Component({
    selector: 'app-settings-shell',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [MatTabsModule, RouterLink, RouterLinkActive, RouterOutlet],
    template: `
        <div class="shell">
            <nav mat-tab-nav-bar [tabPanel]="panel">
                @if (session.can('masterData.write')) {
                    <a
                        mat-tab-link
                        routerLink="/settings/invoice-design"
                        routerLinkActive
                        #invoiceDesignLink="routerLinkActive"
                        [routerLinkActiveOptions]="{ exact: true }"
                        [active]="invoiceDesignLink.isActive"
                        data-testid="settings-tab-invoice-design"
                    >
                        {{ i18n.t('settings.tabs.invoiceDesign') }}
                    </a>
                }
                <a
                    mat-tab-link
                    routerLink="/settings/geodata"
                    routerLinkActive
                    #geodataLink="routerLinkActive"
                    [routerLinkActiveOptions]="{ exact: true }"
                    [active]="geodataLink.isActive"
                    data-testid="settings-tab-geodata"
                >
                    {{ i18n.t('settings.tabs.geodata') }}
                </a>
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
export class SettingsShellComponent {
    protected readonly i18n = inject(I18nService);
    protected readonly session = inject(SessionService);
}
