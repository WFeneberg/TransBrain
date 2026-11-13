import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatTableDataSource, MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { ConfirmDialogComponent, ConfirmDialogData } from '../shared/confirm-dialog.component';
import { InspectionUrgency, formatDate, inspectionUrgency } from '../vehicles/vehicle-format';
import { Driver, DriverService } from './driver.service';

/**
 * The API's maximum page size. Neither frontend has pagination controls yet, so whatever one
 * request returns is all a user can ever see - and lists come back sorted ascending, meaning a
 * default page of 20 shows the twenty OLDEST records and hides everything added since. Asking
 * for the cap is a stopgap, not a fix: real paging controls are still needed above 100 rows.
 */
const LIST_PAGE_SIZE = 100;

const DRIVER_STATUSES = ['Available', 'Absent', 'Inactive'] as const;

interface DriverFilter {
    search: string;
    status: string;
}

@Component({
    selector: 'app-driver-list',
    standalone: true,
    imports: [
        MatButtonModule,
        MatChipsModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatProgressBarModule,
        MatSelectModule,
        MatSortModule,
        MatTableModule,
        MatTooltipModule,
        RouterLink,
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        @if (session.isAuthenticated()) {
            <div class="page">
                <header class="page-header">
                    <div>
                        <h1>{{ i18n.t('nav.drivers') }}</h1>
                        <p class="subtitle" data-testid="driver-count">
                            {{ i18n.t('drivers.count', { count: filteredCount() }) }}
                        </p>
                    </div>
                    <div class="header-actions">
                        <a
                            mat-stroked-button
                            routerLink="/driver-catalog"
                            data-testid="driver-catalog-link"
                        >
                            <mat-icon>tune</mat-icon>
                            {{ i18n.t('drivers.catalogLink') }}
                        </a>
                        @if (session.can('masterData.write')) {
                            <a mat-flat-button routerLink="/drivers/new" data-testid="driver-add">
                                <mat-icon>add</mat-icon>
                                {{ i18n.t('drivers.add') }}
                            </a>
                        }
                    </div>
                </header>

                @if (actionError(); as message) {
                    <p class="error-banner" data-testid="driver-action-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                }

                @if (errorMessage(); as message) {
                    <p class="error-banner" data-testid="driver-list-error">
                        <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                    </p>
                } @else {
                    <section class="filter-bar">
                        <mat-form-field
                            appearance="outline"
                            subscriptSizing="dynamic"
                            class="search"
                        >
                            <mat-label>{{ i18n.t('drivers.search') }}</mat-label>
                            <mat-icon matPrefix>search</mat-icon>
                            <input
                                matInput
                                data-testid="driver-search"
                                [value]="search()"
                                (input)="search.set($any($event.target).value)"
                                [placeholder]="i18n.t('drivers.searchHint')"
                            />
                        </mat-form-field>
                        <mat-form-field appearance="outline" subscriptSizing="dynamic">
                            <mat-label>{{ i18n.t('common.status') }}</mat-label>
                            <mat-select
                                data-testid="driver-status-filter"
                                [value]="statusFilter()"
                                (valueChange)="statusFilter.set($event)"
                            >
                                <mat-option value="">{{ i18n.t('common.all') }}</mat-option>
                                @for (status of statuses; track status) {
                                    <mat-option [value]="status">{{ status }}</mat-option>
                                }
                            </mat-select>
                        </mat-form-field>
                    </section>

                    <section class="table-card">
                        @if (loading()) {
                            <mat-progress-bar mode="indeterminate" />
                        }
                        <div class="table-scroll">
                            <table mat-table [dataSource]="dataSource" matSort>
                                <ng-container matColumnDef="lastName">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('common.lastName') }}
                                    </th>
                                    <td
                                        mat-cell
                                        *matCellDef="let d"
                                        class="strong"
                                        data-testid="driver-lastname"
                                    >
                                        {{ d.lastName }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="firstName">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('common.firstName') }}
                                    </th>
                                    <td mat-cell *matCellDef="let d" data-testid="driver-firstname">
                                        {{ d.firstName }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="licenseClasses">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('common.licenseClasses') }}
                                    </th>
                                    <td mat-cell *matCellDef="let d">
                                        {{ d.licenseClasses.join(', ') }}
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="licenseValidUntil">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('drivers.licenseValidUntil') }}
                                    </th>
                                    <td mat-cell *matCellDef="let d">
                                        @switch (urgency(d)) {
                                            @case ('overdue') {
                                                <mat-chip-set>
                                                    <mat-chip
                                                        class="chip"
                                                        data-tone="error"
                                                        [matTooltip]="
                                                            i18n.t('drivers.licenseExpired')
                                                        "
                                                        >{{ date(d.licenseValidUntil) }}</mat-chip
                                                    >
                                                </mat-chip-set>
                                            }
                                            @case ('dueSoon') {
                                                <mat-chip-set>
                                                    <mat-chip
                                                        class="chip"
                                                        data-tone="warning"
                                                        [matTooltip]="
                                                            i18n.t('drivers.licenseExpiresSoon')
                                                        "
                                                        >{{ date(d.licenseValidUntil) }}</mat-chip
                                                    >
                                                </mat-chip-set>
                                            }
                                            @default {
                                                {{ date(d.licenseValidUntil) }}
                                            }
                                        }
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="status">
                                    <th mat-header-cell *matHeaderCellDef mat-sort-header>
                                        {{ i18n.t('common.status') }}
                                    </th>
                                    <td mat-cell *matCellDef="let d">
                                        <mat-chip-set>
                                            <mat-chip
                                                class="chip"
                                                [attr.data-tone]="statusTone(d.status)"
                                                data-testid="driver-status"
                                                >{{ d.status }}</mat-chip
                                            >
                                        </mat-chip-set>
                                    </td>
                                </ng-container>
                                <ng-container matColumnDef="actions">
                                    <th mat-header-cell *matHeaderCellDef class="actions-cell">
                                        <span class="cdk-visually-hidden">{{
                                            i18n.t('common.actions')
                                        }}</span>
                                    </th>
                                    <td mat-cell *matCellDef="let d" class="actions-cell">
                                        <a
                                            mat-icon-button
                                            [routerLink]="['/drivers', d.id]"
                                            data-testid="driver-edit"
                                            [matTooltip]="i18n.t('common.edit')"
                                            [attr.aria-label]="
                                                i18n.t('common.edit') + ' ' + fullName(d)
                                            "
                                        >
                                            <mat-icon>edit</mat-icon>
                                        </a>
                                        <button
                                            mat-icon-button
                                            class="delete"
                                            data-testid="driver-delete"
                                            [matTooltip]="i18n.t('common.delete')"
                                            [attr.aria-label]="
                                                i18n.t('common.delete') + ' ' + fullName(d)
                                            "
                                            (click)="delete(d)"
                                        >
                                            <mat-icon>delete</mat-icon>
                                        </button>
                                    </td>
                                </ng-container>
                                <tr mat-header-row *matHeaderRowDef="columns()"></tr>
                                <tr mat-row *matRowDef="let row; columns: columns()"></tr>
                                <tr class="mat-mdc-row" *matNoDataRow>
                                    <td class="empty" [attr.colspan]="columns().length">
                                        @if (!loading()) {
                                            <mat-icon aria-hidden="true">person_off</mat-icon>
                                            <span data-testid="driver-empty">{{
                                                i18n.t('drivers.empty')
                                            }}</span>
                                        }
                                    </td>
                                </tr>
                            </table>
                        </div>
                    </section>
                }
            </div>
        } @else {
            <p>{{ i18n.t('drivers.signIn') }}</p>
        }
    `,
})
export class DriverListComponent {
    private readonly service = inject(DriverService);
    private readonly dialog = inject(MatDialog);
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly statuses = DRIVER_STATUSES;
    // Edit and delete need masterData.write - the same gate as the vehicle list and the
    // /drivers/:id route guard, so a viewer is not offered actions that would only end in a 403.
    protected readonly columns = computed(() => {
        const base = ['lastName', 'firstName', 'licenseClasses', 'licenseValidUntil', 'status'];
        return this.session.can('masterData.write') ? [...base, 'actions'] : base;
    });
    protected readonly drivers = signal<Driver[]>([]);
    protected readonly loading = signal(false);
    protected readonly errorMessage = signal<string | null>(null);
    // Separate from errorMessage: a failed delete must not hide the table the way a failed
    // list load does (mirrors VehicleListComponent's actionError - see that file for why).
    protected readonly actionError = signal<string | null>(null);

    protected readonly search = signal('');
    protected readonly statusFilter = signal('');

    private readonly criteria = computed<DriverFilter>(() => ({
        search: this.search().trim().toLowerCase(),
        status: this.statusFilter(),
    }));
    protected readonly filteredCount = computed(() => {
        const criteria = this.criteria();
        return this.drivers().filter((driver) => this.matches(driver, criteria)).length;
    });

    protected readonly dataSource = new MatTableDataSource<Driver>([]);
    private readonly sort = viewChild(MatSort);

    constructor() {
        this.dataSource.filterPredicate = (driver, filterJson) =>
            this.matches(driver, JSON.parse(filterJson) as DriverFilter);
        this.dataSource.sortingDataAccessor = (driver, column) => this.sortValue(driver, column);

        effect(() => {
            this.dataSource.data = this.drivers();
        });
        effect(() => {
            this.dataSource.filter = JSON.stringify(this.criteria());
        });
        effect(() => {
            this.dataSource.sort = this.sort() ?? null;
        });

        this.session.ready.subscribe((isAuthenticated) => {
            if (isAuthenticated) {
                this.refresh();
            }
        });
    }

    protected statusTone(status: string): string {
        switch (status) {
            case 'Available':
                return 'success';
            case 'Absent':
                return 'warning';
            default:
                return 'neutral';
        }
    }

    protected fullName(driver: Driver): string {
        return `${driver.firstName} ${driver.lastName}`;
    }

    protected date(iso: string): string {
        return formatDate(iso, this.i18n.locale());
    }

    protected urgency(driver: Driver): InspectionUrgency {
        return inspectionUrgency(driver.licenseValidUntil);
    }

    protected delete(driver: Driver): void {
        this.actionError.set(null);
        const data: ConfirmDialogData = {
            title: this.i18n.t('drivers.deleteTitle'),
            message: this.i18n.t('drivers.deleteConfirm', { name: this.fullName(driver) }),
            confirmLabel: this.i18n.t('common.delete'),
            cancelLabel: this.i18n.t('common.cancel'),
            confirmTestId: 'driver-delete-confirm',
            cancelTestId: 'driver-delete-cancel',
            icon: 'delete',
        };
        this.dialog
            .open<ConfirmDialogComponent, ConfirmDialogData, boolean>(ConfirmDialogComponent, {
                data,
                width: '440px',
            })
            .afterClosed()
            .pipe(
                filter((confirmed) => confirmed === true),
                switchMap(() => this.service.remove(driver.id)),
            )
            .subscribe({
                next: () => this.refresh(),
                // A policy failure (e.g. a non-admin's 403) is rejected by ASP.NET's authorization
                // middleware before the endpoint runs, so it carries no ProblemDetails body at all -
                // describe()'s fallback text is therefore action-specific, not the list-load one.
                error: (error: HttpErrorResponse) =>
                    this.actionError.set(this.describe(error, this.i18n.t('drivers.error.delete'))),
            });
    }

    private refresh(): void {
        this.loading.set(true);
        this.service.list(LIST_PAGE_SIZE).subscribe({
            next: (page) => {
                this.drivers.set(page.items);
                this.loading.set(false);
            },
            error: (error: HttpErrorResponse) => {
                this.loading.set(false);
                this.errorMessage.set(this.describe(error, this.i18n.t('drivers.error.list')));
            },
        });
    }

    private matches(driver: Driver, criteria: DriverFilter): boolean {
        if (criteria.status && driver.status !== criteria.status) {
            return false;
        }
        if (!criteria.search) {
            return true;
        }
        return [driver.firstName, driver.lastName, driver.licenseClasses.join(' ')].some((value) =>
            value.toLowerCase().includes(criteria.search),
        );
    }

    private sortValue(driver: Driver, column: string): string | number {
        switch (column) {
            case 'licenseClasses':
                return driver.licenseClasses.join(', ').toLowerCase();
            case 'licenseValidUntil':
                return driver.licenseValidUntil ?? '';
            default: {
                const value = (driver as unknown as Record<string, unknown>)[column];
                return typeof value === 'string' ? value.toLowerCase() : String(value ?? '');
            }
        }
    }

    // See VehicleListComponent.describe: same reasoning, same now-superseded Phase 1 history.
    private describe(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as { title?: string; detail?: string } | null;
        const sentence = problem?.detail ?? problem?.title ?? fallback;
        return `${sentence} (HTTP ${error.status})`;
    }
}
