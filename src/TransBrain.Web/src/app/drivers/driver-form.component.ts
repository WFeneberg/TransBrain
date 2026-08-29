import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ActivatedRoute, Router } from '@angular/router';
import { Driver, DriverService, DriverWriteRequest } from './driver.service';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

@Component({
    selector: 'app-driver-form',
    standalone: true,
    imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule],
    template: `
        <h1>{{ isEditMode ? 'Edit driver' : 'New driver' }}</h1>
        @if (formError(); as message) {
            <p data-testid="driver-form-error">{{ message }}</p>
        }
        <form [formGroup]="form" (ngSubmit)="save()">
            <mat-form-field>
                <mat-label>First name</mat-label>
                <input matInput formControlName="firstName" data-testid="driver-firstName" />
            </mat-form-field>
            <mat-form-field>
                <mat-label>Last name</mat-label>
                <input matInput formControlName="lastName" data-testid="driver-lastName" />
            </mat-form-field>
            <fieldset>
                <legend>License classes</legend>
                @for (licenseClass of licenseClassOptions; track licenseClass) {
                    <label>
                        <input
                            type="checkbox"
                            [checked]="isLicenseClassSelected(licenseClass)"
                            (change)="toggleLicenseClass(licenseClass)"
                            [attr.data-testid]="'driver-licenseClass-' + licenseClass"
                        />
                        {{ licenseClass }}
                    </label>
                }
            </fieldset>
            <mat-form-field>
                <mat-label>License valid until</mat-label>
                <input
                    matInput
                    type="date"
                    formControlName="licenseValidUntil"
                    data-testid="driver-licenseValidUntil"
                />
            </mat-form-field>
            <button mat-raised-button type="submit" data-testid="driver-save">Save</button>
            <button mat-button type="button" data-testid="driver-cancel" (click)="cancel()">Cancel</button>
        </form>
    `,
})
export class DriverFormComponent {
    private readonly service = inject(DriverService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly fb = inject(FormBuilder);

    protected readonly licenseClassOptions = ['B', 'C1', 'C', 'CE'];
    protected readonly driverId = this.route.snapshot.paramMap.get('id');
    protected readonly isEditMode = this.driverId !== null;
    protected readonly formError = signal<string | null>(null);
    private externalUserId: string | null = null;

    protected readonly form = this.fb.nonNullable.group({
        firstName: ['', Validators.required],
        lastName: ['', Validators.required],
        licenseClasses: this.fb.nonNullable.control<string[]>([], Validators.required),
        licenseValidUntil: ['', Validators.required],
    });

    constructor() {
        if (this.isEditMode) {
            this.service.getById(this.driverId!).subscribe({
                next: (driver: Driver) => {
                    this.externalUserId = driver.externalUserId;
                    this.form.patchValue({
                        firstName: driver.firstName,
                        lastName: driver.lastName,
                        licenseClasses: driver.licenseClasses,
                        licenseValidUntil: driver.licenseValidUntil,
                    });
                },
                error: (error: HttpErrorResponse) => this.formError.set(this.describeFailure(error)),
            });
        }
    }

    protected isLicenseClassSelected(licenseClass: string): boolean {
        return this.form.controls.licenseClasses.value.includes(licenseClass);
    }

    protected toggleLicenseClass(licenseClass: string): void {
        const current = this.form.controls.licenseClasses.value;
        const next = current.includes(licenseClass)
            ? current.filter((c) => c !== licenseClass)
            : [...current, licenseClass];
        this.form.controls.licenseClasses.setValue(next);
    }

    protected save(): void {
        this.formError.set(null);
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        const request: DriverWriteRequest = { ...this.form.getRawValue(), externalUserId: this.externalUserId };
        const save$ = this.isEditMode
            ? this.service.update(this.driverId!, request)
            : this.service.create(request);

        save$.subscribe({
            next: () => this.router.navigateByUrl('/drivers'),
            error: (error: HttpErrorResponse) => this.applyServerErrors(error),
        });
    }

    protected cancel(): void {
        this.router.navigateByUrl('/drivers');
    }

    // Phase 1 forbade binding ProblemDetails' `errors` dictionary here, because it was keyed
    // by error code rather than by field name. Task 1 of the 2026-08-29 master-data-completion
    // phase fixed the API to group every failure under its real field name instead, so binding
    // it is now correct - this supersedes that earlier rule. See VehicleFormComponent for the
    // same comment in full; the PascalCase-to-camelCase key lowercasing works the same way here.
    private applyServerErrors(error: HttpErrorResponse): void {
        const problem = error.error as ProblemDetailsBody | null;

        if (problem?.errors) {
            for (const [field, messages] of Object.entries(problem.errors)) {
                const controlName = field.charAt(0).toLowerCase() + field.slice(1);
                const control = this.form.get(controlName);
                const message = messages.join(' ');
                if (control) {
                    control.setErrors({ server: message });
                } else {
                    this.formError.update((existing) => (existing ? `${existing} ${message}` : message));
                }
            }
            return;
        }

        this.formError.set(this.describeFailure(error));
    }

    // No `errors` dictionary means a domain-level failure (404, 409, or a rejected domain
    // invariant) rather than a per-field one - `detail` is the stable, human-readable message
    // for all of those. See VehicleFormComponent.describeFailure for the full reasoning.
    private describeFailure(error: HttpErrorResponse): string {
        const problem = error.error as ProblemDetailsBody | null;
        const message = problem?.detail ?? problem?.title ?? 'The driver could not be saved.';
        return `${message} (HTTP ${error.status})`;
    }
}
