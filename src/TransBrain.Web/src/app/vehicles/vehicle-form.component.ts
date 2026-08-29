import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router } from '@angular/router';
import { Vehicle, VehicleService, VehicleWriteRequest } from './vehicle.service';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

@Component({
    selector: 'app-vehicle-form',
    standalone: true,
    imports: [ReactiveFormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
    template: `
        <h1>{{ isEditMode ? 'Edit vehicle' : 'New vehicle' }}</h1>
        @if (formError(); as message) {
            <p data-testid="vehicle-form-error">{{ message }}</p>
        }
        <form [formGroup]="form" (ngSubmit)="save()">
            <mat-form-field>
                <mat-label>License plate</mat-label>
                <input matInput formControlName="licensePlate" data-testid="vehicle-licensePlate" />
            </mat-form-field>
            <mat-form-field>
                <mat-label>Type</mat-label>
                <mat-select formControlName="type" data-testid="vehicle-type">
                    @for (option of vehicleTypes; track option) {
                        <mat-option [value]="option">{{ option }}</mat-option>
                    }
                </mat-select>
            </mat-form-field>
            <mat-form-field>
                <mat-label>Payload (kg)</mat-label>
                <input matInput type="number" formControlName="payloadKg" data-testid="vehicle-payloadKg" />
            </mat-form-field>
            <mat-form-field>
                <mat-label>Load meters</mat-label>
                <input matInput type="number" formControlName="loadMeters" data-testid="vehicle-loadMeters" />
            </mat-form-field>
            <mat-form-field>
                <mat-label>Next inspection due</mat-label>
                <input
                    matInput
                    type="date"
                    formControlName="nextInspectionDue"
                    data-testid="vehicle-nextInspectionDue"
                />
            </mat-form-field>
            <button mat-raised-button type="submit" data-testid="vehicle-save">Save</button>
            <button mat-button type="button" data-testid="vehicle-cancel" (click)="cancel()">Cancel</button>
        </form>
    `,
})
export class VehicleFormComponent {
    private readonly service = inject(VehicleService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly fb = inject(FormBuilder);

    protected readonly vehicleTypes = ['Tractor', 'RigidTruck', 'Van'];
    protected readonly vehicleId = this.route.snapshot.paramMap.get('id');
    protected readonly isEditMode = this.vehicleId !== null;
    protected readonly formError = signal<string | null>(null);

    protected readonly form = this.fb.nonNullable.group({
        licensePlate: ['', Validators.required],
        type: ['Tractor', Validators.required],
        payloadKg: [0, Validators.required],
        loadMeters: [0, Validators.required],
        nextInspectionDue: ['', Validators.required],
    });

    constructor() {
        if (this.isEditMode) {
            this.service.getById(this.vehicleId!).subscribe({
                next: (vehicle: Vehicle) =>
                    this.form.patchValue({
                        licensePlate: vehicle.licensePlate,
                        type: vehicle.type,
                        payloadKg: vehicle.payloadKg,
                        loadMeters: vehicle.loadMeters,
                        nextInspectionDue: vehicle.nextInspectionDue,
                    }),
                error: (error: HttpErrorResponse) => this.formError.set(this.describeFailure(error)),
            });
        }
    }

    protected save(): void {
        this.formError.set(null);
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        const request: VehicleWriteRequest = this.form.getRawValue();
        const save$ = this.isEditMode
            ? this.service.update(this.vehicleId!, request)
            : this.service.create(request);

        save$.subscribe({
            next: () => this.router.navigateByUrl('/vehicles'),
            error: (error: HttpErrorResponse) => this.applyServerErrors(error),
        });
    }

    protected cancel(): void {
        this.router.navigateByUrl('/vehicles');
    }

    // Phase 1 forbade binding ProblemDetails' `errors` dictionary here, because it was keyed
    // by error code (e.g. "Vehicle.PayloadKgNotPositive") rather than by the field name a form
    // control could match against. Task 1 of the 2026-08-29 master-data-completion phase fixed
    // the API to group every failure under its real field name instead, so binding it is now
    // correct - this supersedes that earlier rule. The dictionary key is still the .NET
    // property name (PascalCase, e.g. "LicensePlate"), while this form's controls use the
    // camelCase names the rest of the wire format uses, so each key is lowercased on its first
    // character before the control lookup.
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
                    // A failure keyed to something this form has no control for still needs
                    // to reach the user - fall back to the form-level message.
                    this.formError.update((existing) => (existing ? `${existing} ${message}` : message));
                }
            }
            return;
        }

        this.formError.set(this.describeFailure(error));
    }

    // No `errors` dictionary means this was not a per-field validation failure but a
    // domain-level one (404 not found, 409 conflict on a duplicate plate, or a domain
    // invariant rejected before any field-level validator ran) - `detail` is the stable,
    // human-readable message for all of those. `errorCode` (present only on the domain-
    // invariant 400 case) is available for callers that want to branch on it; none of the
    // vehicle failures need a friendlier message than `detail` already gives.
    private describeFailure(error: HttpErrorResponse): string {
        const problem = error.error as ProblemDetailsBody | null;
        const message = problem?.detail ?? problem?.title ?? 'The vehicle could not be saved.';
        return `${message} (HTTP ${error.status})`;
    }
}
