import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { switchMap } from 'rxjs';
import { Driver, DriverService } from '../drivers/driver.service';
import { Vehicle, VehicleService } from '../vehicles/vehicle.service';
import { TourService } from './tour.service';

/** The API's maximum page size. A picker must offer every choice, not the first twenty. */
const PICKER_PAGE_SIZE = 100;

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

@Component({
    selector: 'app-tour-form',
    standalone: true,
    imports: [
        ReactiveFormsModule,
        MatButtonModule,
        MatCardModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatSelectModule,
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        <div class="page">
            <header class="page-header align-start">
                <div class="title-row">
                    <h1>{{ i18n.t('tours.new') }}</h1>
                </div>
            </header>

            @if (formError(); as message) {
                <p class="error-banner" data-testid="tour-form-error">
                    <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                </p>
            }

            <form [formGroup]="form" (ngSubmit)="save()">
                <div class="cards">
                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">event</mat-icon>
                            <mat-card-title>{{ i18n.t('tours.tourDate') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields one">
                            <mat-form-field>
                                <mat-label>{{ i18n.t('tours.tourDate') }}</mat-label>
                                <input
                                    matInput
                                    type="date"
                                    formControlName="tourDate"
                                    data-testid="tour-tourDate"
                                />
                                @if (form.controls.tourDate.errors; as errors) {
                                    <mat-error data-testid="tour-tourDate-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">local_shipping</mat-icon>
                            <mat-card-title>{{ i18n.t('tours.section.crew') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            <mat-form-field>
                                <mat-label>{{ i18n.t('common.vehicle') }}</mat-label>
                                <mat-select
                                    formControlName="vehicleId"
                                    data-testid="tour-vehicleId"
                                >
                                    @for (vehicle of vehicles(); track vehicle.id) {
                                        <mat-option [value]="vehicle.id">{{
                                            vehicle.licensePlate
                                        }}</mat-option>
                                    }
                                </mat-select>
                                @if (form.controls.vehicleId.errors; as errors) {
                                    <mat-error data-testid="tour-vehicleId-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('common.driver') }}</mat-label>
                                <mat-select formControlName="driverId" data-testid="tour-driverId">
                                    @for (driver of drivers(); track driver.id) {
                                        <mat-option [value]="driver.id"
                                            >{{ driver.lastName }},
                                            {{ driver.firstName }}</mat-option
                                        >
                                    }
                                </mat-select>
                                @if (form.controls.driverId.errors; as errors) {
                                    <mat-error data-testid="tour-driverId-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>
                </div>

                <div class="action-bar">
                    <button mat-button type="button" data-testid="tour-cancel" (click)="cancel()">
                        {{ i18n.t('common.cancel') }}
                    </button>
                    <button mat-flat-button type="submit" data-testid="tour-save">
                        <mat-icon>save</mat-icon>
                        {{ i18n.t('common.save') }}
                    </button>
                </div>
            </form>
        </div>
    `,
})
export class TourFormComponent {
    private readonly service = inject(TourService);
    private readonly vehicleService = inject(VehicleService);
    private readonly driverService = inject(DriverService);
    private readonly router = inject(Router);
    private readonly fb = inject(FormBuilder);
    // SessionService.ready is what guarantees angular-auth-oidc-client has rehydrated its
    // stored session before the first request goes out. A form reached by a DIRECT navigation -
    // a bookmark, a page reload while editing, browser back - has no list component in its
    // lifetime, so without this every request from it answered 401 to a plainly signed-in user.
    // The one checkAuth() behind it runs in the App component.
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly vehicles = signal<Vehicle[]>([]);
    protected readonly drivers = signal<Driver[]>([]);
    protected readonly formError = signal<string | null>(null);

    protected readonly form = this.fb.nonNullable.group({
        tourDate: ['', Validators.required],
        vehicleId: ['', Validators.required],
        driverId: ['', Validators.required],
    });

    constructor() {
        this.session.ready
            .pipe(switchMap(() => this.vehicleService.list(PICKER_PAGE_SIZE)))
            .subscribe({
                next: (page) =>
                    this.vehicles.set(page.items.filter((vehicle) => vehicle.type === 'Tractor')),
                error: (error: HttpErrorResponse) =>
                    this.formError.set(
                        this.describeFailure(error, this.i18n.t('vehicles.error.list')),
                    ),
            });

        this.session.ready
            .pipe(switchMap(() => this.driverService.list(PICKER_PAGE_SIZE)))
            .subscribe({
                next: (page) => this.drivers.set(page.items),
                error: (error: HttpErrorResponse) =>
                    this.formError.set(
                        this.describeFailure(error, this.i18n.t('drivers.error.list')),
                    ),
            });
    }

    // See DriverFormComponent.fieldErrorText for the full reasoning.
    protected fieldErrorText(errors: ValidationErrors): string {
        if (errors['server']) {
            return errors['server'] as string;
        }
        if (errors['required']) {
            return this.i18n.t('common.fieldRequired');
        }
        return this.i18n.t('common.fieldInvalid');
    }

    protected save(): void {
        this.formError.set(null);
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        this.session.ready
            .pipe(switchMap(() => this.service.create(this.form.getRawValue())))
            .subscribe({
                // To the detail page, not the list: a dispatcher's next action after planning a
                // tour is always to put orders on it.
                next: (tour) => this.router.navigateByUrl(`/tours/${tour.id}`),
                error: (error: HttpErrorResponse) => this.applyServerErrors(error),
            });
    }

    protected cancel(): void {
        this.router.navigateByUrl('/tours');
    }

    // See OrderFormComponent.applyServerErrors: binding the field-keyed `errors` dictionary is
    // correct and supersedes a Phase 1 instruction that forbade it.
    private applyServerErrors(error: HttpErrorResponse): void {
        const problem = error.error as ProblemDetailsBody | null;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const path = field
                    .split('.')
                    .map((segment) => segment.charAt(0).toLowerCase() + segment.slice(1))
                    .join('.');
                const control = this.form.get(path);
                const message = messages.join(' ');
                if (control) {
                    control.setErrors({ server: message });
                    control.markAsTouched();
                } else {
                    this.formError.update((existing) =>
                        existing ? `${existing} ${message}` : message,
                    );
                }
            }
            return;
        }

        // A double booking, an unavailable vehicle and an expired licence all arrive here as a
        // 409 with a detail message naming the reason - which is exactly what the dispatcher
        // needs to see, so it is shown verbatim rather than replaced with a generic sentence.
        this.formError.set(this.describeFailure(error, this.i18n.t('tours.error.save')));
    }

    private describeFailure(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as ProblemDetailsBody | null;
        const message = problem?.detail ?? problem?.title ?? fallback;
        return `${message} (HTTP ${error.status})`;
    }
}
