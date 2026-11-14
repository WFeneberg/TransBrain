import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { I18nService } from '../i18n/i18n.service';
import { SystemTenantService, TenantSummary } from '../system/system-tenant.service';

const GUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * Shell of the global Wartung (maintenance) area, reserved for system administrators. A system
 * admin belongs to no tenant, so the shell's first job is choosing the tenant to work ON: picked
 * from the list of tenants owning data, or entered as a GUID for a tenant that has none yet.
 * Only with a choice made do the tool tabs (backup/restore, import, history, export) and their
 * pages appear — every one of them acts on exactly the chosen tenant.
 */
@Component({
    selector: 'app-maintenance-shell',
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        MatButtonModule,
        MatCardModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatTabsModule,
        RouterLink,
        RouterLinkActive,
        RouterOutlet,
    ],
    template: `
        <div class="shell">
            <mat-card appearance="outlined" class="tenant-card">
                <mat-card-header>
                    <mat-card-title>{{ i18n.t('maintenance.tenant.title') }}</mat-card-title>
                    <mat-card-subtitle>{{ i18n.t('maintenance.tenant.description') }}</mat-card-subtitle>
                </mat-card-header>
                <mat-card-content>
                    @if (loadError()) {
                        <p class="load-error" data-testid="system-tenant-error">
                            {{ i18n.t('maintenance.tenant.loadError') }}
                        </p>
                    }
                    <div class="pickers">
                        <mat-form-field appearance="outline" class="tenant-select">
                            <mat-label>{{ i18n.t('maintenance.tenant.select') }}</mat-label>
                            <mat-select
                                [value]="tenant.selectedTenantId()"
                                (selectionChange)="choose($event.value)"
                                data-testid="system-tenant-select"
                            >
                                @for (entry of tenants(); track entry.tenantId) {
                                    <mat-option [value]="entry.tenantId">
                                        {{ entry.tenantId }} ·
                                        {{ i18n.t('maintenance.tenant.rows', { rows: entry.totalRows }) }}
                                    </mat-option>
                                } @empty {
                                    <mat-option [value]="null" disabled>
                                        {{ i18n.t('maintenance.tenant.none') }}
                                    </mat-option>
                                }
                            </mat-select>
                        </mat-form-field>
                        <span class="or">{{ i18n.t('maintenance.tenant.or') }}</span>
                        <mat-form-field appearance="outline" class="manual">
                            <mat-label>{{ i18n.t('maintenance.tenant.manual') }}</mat-label>
                            <input
                                matInput
                                [value]="manualId()"
                                (input)="manualId.set(manualInput.value)"
                                #manualInput
                                placeholder="00000000-0000-0000-0000-000000000000"
                                data-testid="system-tenant-manual"
                            />
                            @if (manualInvalid()) {
                                <mat-error>{{ i18n.t('maintenance.tenant.invalid') }}</mat-error>
                            }
                        </mat-form-field>
                        <button
                            mat-stroked-button
                            type="button"
                            (click)="applyManual()"
                            data-testid="system-tenant-apply"
                        >
                            {{ i18n.t('maintenance.tenant.apply') }}
                        </button>
                    </div>
                    @if (tenant.hasSelection()) {
                        <p class="active" data-testid="system-tenant-active">
                            {{ i18n.t('maintenance.tenant.current', { id: tenant.selectedTenantId()! }) }}
                        </p>
                    }
                </mat-card-content>
            </mat-card>

            @if (tenant.hasSelection()) {
                <nav mat-tab-nav-bar [tabPanel]="panel">
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
                    <a
                        mat-tab-link
                        routerLink="/maintenance/export"
                        routerLinkActive
                        #exportLink="routerLinkActive"
                        [routerLinkActiveOptions]="{ exact: true }"
                        [active]="exportLink.isActive"
                        data-testid="maintenance-tab-export"
                    >
                        {{ i18n.t('maintenance.tabs.export') }}
                    </a>
                </nav>
                <mat-tab-nav-panel #panel>
                    <router-outlet />
                </mat-tab-nav-panel>
            } @else {
                <p class="pick-first" data-testid="system-tenant-required">
                    {{ i18n.t('maintenance.tenant.required') }}
                </p>
            }
        </div>
    `,
    styles: `
        .shell {
            max-width: 1200px;
            margin: 0 auto;
        }

        .tenant-card {
            margin-bottom: 24px;
        }

        .pickers {
            display: flex;
            align-items: baseline;
            gap: 16px;
            flex-wrap: wrap;
            margin-top: 8px;
        }

        .tenant-select {
            min-width: 340px;
        }

        .manual {
            min-width: 340px;
        }

        .or {
            color: var(--mat-sys-on-surface-variant);
        }

        .active {
            margin: 0;
            font-weight: 500;
        }

        .load-error {
            color: var(--mat-sys-error);
        }

        .pick-first {
            text-align: center;
            color: var(--mat-sys-on-surface-variant);
            margin-top: 48px;
        }

        nav {
            margin-bottom: 24px;
        }
    `,
})
export class MaintenanceShellComponent {
    protected readonly i18n = inject(I18nService);
    protected readonly tenant = inject(SystemTenantService);

    protected readonly tenants = signal<TenantSummary[]>([]);
    protected readonly loadError = signal(false);
    protected readonly manualId = signal('');
    protected readonly manualInvalid = signal(false);

    constructor() {
        this.tenant.getTenants().subscribe({
            next: (tenants) => this.tenants.set(tenants),
            error: () => this.loadError.set(true),
        });
    }

    protected choose(tenantId: string | null): void {
        if (tenantId !== null) {
            this.manualId.set('');
            this.manualInvalid.set(false);
            this.tenant.select(tenantId);
        }
    }

    protected applyManual(): void {
        const id = this.manualId().trim();
        if (!GUID_PATTERN.test(id) || /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(id)) {
            this.manualInvalid.set(true);
            return;
        }
        this.manualInvalid.set(false);
        this.tenant.select(id.toLowerCase());
    }
}
