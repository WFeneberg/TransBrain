import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Router } from '@angular/router';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { shareReplay, switchMap } from 'rxjs';
import { Driver, DriverService } from '../drivers/driver.service';
import { Vehicle, VehicleService } from '../vehicles/vehicle.service';
import { TourService } from './tour.service';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

@Component({
    selector: 'app-tour-form',
    standalone: true,
    imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
    template: `
        <h1>New tour</h1>
        @if (formError(); as message) {
            <p data-testid="tour-form-error">{{ message }}</p>
        }
        <form [formGroup]="form" (ngSubmit)="save()">
            <mat-form-field>
                <mat-label>Tour date</mat-label>
                <input matInput type="date" formControlName="tourDate" data-testid="tour-tourDate" />
                @if (form.controls.tourDate.errors; as errors) {
                    <mat-error data-testid="tour-tourDate-error">{{ fieldErrorText(errors) }}</mat-error>
                }
            </mat-form-field>
            <mat-form-field>
                <mat-label>Vehicle</mat-label>
                <mat-select formControlName="vehicleId" data-testid="tour-vehicleId">
                    @for (vehicle of vehicles(); track vehicle.id) {
                        <mat-option [value]="vehicle.id">{{ vehicle.licensePlate }}</mat-option>
                    }
                </mat-select>
                @if (form.controls.vehicleId.errors; as errors) {
                    <mat-error data-testid="tour-vehicleId-error">{{ fieldErrorText(errors) }}</mat-error>
                }
            </mat-form-field>
            <mat-form-field>
                <mat-label>Driver</mat-label>
                <mat-select formControlName="driverId" data-testid="tour-driverId">
                    @for (driver of drivers(); track driver.id) {
                        <mat-option [value]="driver.id">{{ driver.lastName }}, {{ driver.firstName }}</mat-option>
                    }
                </mat-select>
                @if (form.controls.driverId.errors; as errors) {
                    <mat-error data-testid="tour-driverId-error">{{ fieldErrorText(errors) }}</mat-error>
                }
            </mat-form-field>
            <button mat-raised-button type="submit" data-testid="tour-save">Save</button>
            <button mat-button type="button" data-testid="tour-cancel" (click)="cancel()">Cancel</button>
        </form>
    `,
})
export class TourFormComponent {
    private readonly service = inject(TourService);
    private readonly vehicleService = inject(VehicleService);
    private readonly driverService = inject(DriverService);
    private readonly router = inject(Router);
    private readonly fb = inject(FormBuilder);
    private readonly oidc = inject(OidcSecurityService);

    // angular-auth-oidc-client only hydrates its stored session when checkAuth() runs, and until
    // it has, the auth interceptor sends the request unauthenticated. The list components call
    // it on construction; a form reached by a DIRECT navigation - a bookmarked /tours/new, a
    // page reload - has no list component in its lifetime, so every request from it would
    // answer 401 to a plainly signed-in dispatcher. Same fix as OrderFormComponent.
    private readonly session = this.oidc.checkAuth().pipe(shareReplay(1));

    protected readonly vehicles = signal<Vehicle[]>([]);
    protected readonly drivers = signal<Driver[]>([]);
    protected readonly formError = signal<string | null>(null);

    protected readonly form = this.fb.nonNullable.group({
        tourDate: ['', Validators.required],
        vehicleId: ['', Validators.required],
        driverId: ['', Validators.required],
    });

    constructor() {
        this.session.pipe(switchMap(() => this.vehicleService.list())).subscribe({
            next: (page) => this.vehicles.set(page.items),
            error: (error: HttpErrorResponse) =>
                this.formError.set(this.describeFailure(error, 'The vehicle list could not be loaded.')),
        });

        this.session.pipe(switchMap(() => this.driverService.list())).subscribe({
            next: (page) => this.drivers.set(page.items),
            error: (error: HttpErrorResponse) =>
                this.formError.set(this.describeFailure(error, 'The driver list could not be loaded.')),
        });
    }

    // See DriverFormComponent.fieldErrorText for the full reasoning.
    protected fieldErrorText(errors: ValidationErrors): string {
        if (errors['server']) {
            return errors['server'] as string;
        }
        if (errors['required']) {
            return 'This field is required.';
        }
        return 'This field is invalid.';
    }

    protected save(): void {
        this.formError.set(null);
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        this.session
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
                    this.formError.update((existing) => (existing ? `${existing} ${message}` : message));
                }
            }
            return;
        }

        // A double booking, an unavailable vehicle and an expired licence all arrive here as a
        // 409 with a detail message naming the reason - which is exactly what the dispatcher
        // needs to see, so it is shown verbatim rather than replaced with a generic sentence.
        this.formError.set(this.describeFailure(error, 'The tour could not be saved.'));
    }

    private describeFailure(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as ProblemDetailsBody | null;
        const message = problem?.detail ?? problem?.title ?? fallback;
        return `${message} (HTTP ${error.status})`;
    }
}
