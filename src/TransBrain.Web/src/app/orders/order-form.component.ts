import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { I18nService } from '../i18n/i18n.service';
import { LocationMapComponent, MapMarker } from '../shared/location-map.component';
import { ConfirmDialogComponent, ConfirmDialogData } from '../shared/confirm-dialog.component';
import { CountrySelectComponent } from '../shared/country-select.component';
import { Observable, filter, forkJoin, switchMap } from 'rxjs';
import {
    VehicleCatalogEntry,
    VehicleCatalogService,
    catalogIndex,
    catalogName,
} from '../vehicles/vehicle-catalog.service';
import { formatDateTime } from '../vehicles/vehicle-format';
import {
    LocatedParty,
    Order,
    OrderCharge,
    OrderLocations,
    OrderService,
    OrderToll,
    OrderWriteRequest,
} from './order.service';
import {
    OrderChargeDialogComponent,
    OrderChargeDialogData,
} from './order-charge-dialog.component';
import { OrderTollDialogComponent, OrderTollDialogData } from './order-toll-dialog.component';
import {
    CustomerLocationOption,
    CustomerLocationService,
} from '../customers/customer-location.service';

interface ProblemDetailsBody {
    errors?: Record<string, string[]>;
    detail?: string;
    title?: string;
    errorCode?: string;
}

const NOTES_MAX_LENGTH = 2000;
const TOLL_COLUMNS = [
    'category',
    'emptyKm',
    'loadedKm',
    'factorPerKm',
    'emptyAmount',
    'loadedAmount',
    'modified',
] as const;
const CHARGE_COLUMNS = ['description', 'quantity', 'unit', 'unitPrice', 'total', 'modified'] as const;

@Component({
    selector: 'app-order-form',
    standalone: true,
    imports: [
        ReactiveFormsModule,
        DecimalPipe,
        MatButtonModule,
        MatCardModule,
        MatCheckboxModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatTableModule,
        MatTooltipModule,
        LocationMapComponent,
        CountrySelectComponent,
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        <div class="page">
            <header class="page-header align-start">
                <div class="title-row">
                    <h1>{{ isEditMode ? i18n.t('orders.edit') : i18n.t('orders.new') }}</h1>
                </div>
            </header>

            @if (formError(); as message) {
                <p class="error-banner" data-testid="order-form-error">
                    <mat-icon aria-hidden="true">error_outline</mat-icon>{{ message }}
                </p>
            }

            <form [formGroup]="form" (ngSubmit)="save()">
                <div class="cards">
                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">outbox</mat-icon>
                            <mat-card-title>{{ i18n.t('orders.consignor') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields" formGroupName="consignor">
                            @for (field of addressFields; track field.name) {
                                @if (field.name === 'country') {
                                    <app-country-select
                                        [control]="form.controls.consignor.controls.country"
                                        [label]="i18n.t(field.label)"
                                        testid="order-consignor-country"
                                    />
                                } @else {
                                    <mat-form-field [class.span-2]="field.name === 'name'">
                                        <mat-label>{{ i18n.t(field.label) }}</mat-label>
                                        <input
                                            matInput
                                            [formControlName]="field.name"
                                            [attr.data-testid]="'order-consignor-' + field.name"
                                        />
                                        @if (
                                            form.controls.consignor.controls[field.name].errors;
                                            as errors
                                        ) {
                                            <mat-error
                                                [attr.data-testid]="
                                                    'order-consignor-' + field.name + '-error'
                                                "
                                                >{{ fieldErrorText(errors) }}</mat-error
                                            >
                                        }
                                    </mat-form-field>
                                }
                            }
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">move_to_inbox</mat-icon>
                            <mat-card-title>{{ i18n.t('orders.consignee') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            <mat-form-field class="span-2">
                                <mat-label>{{ i18n.t('orders.consigneeLocation') }}</mat-label>
                                <select
                                    matNativeControl
                                    formControlName="consigneeLocationId"
                                    (change)="onLocationChange($event)"
                                    data-testid="order-consigneeLocation"
                                >
                                    <option value="">
                                        {{ i18n.t('orders.consigneeLocation.none') }}
                                    </option>
                                    @for (option of locationOptions(); track option.id) {
                                        <option [value]="option.id">
                                            {{ option.customerName }}: {{ option.name }},
                                            {{ option.city }}
                                        </option>
                                    }
                                </select>
                            </mat-form-field>
                            @if (locationPickerError(); as message) {
                                <p class="hint span-2" data-testid="order-consigneeLocation-error">
                                    <mat-icon aria-hidden="true">info</mat-icon>{{ message }}
                                </p>
                            }
                        </mat-card-content>
                        <mat-card-content class="fields" formGroupName="consignee">
                            @for (field of addressFields; track field.name) {
                                @if (field.name === 'country') {
                                    <app-country-select
                                        [control]="form.controls.consignee.controls.country"
                                        [label]="i18n.t(field.label)"
                                        testid="order-consignee-country"
                                    />
                                } @else {
                                    <mat-form-field [class.span-2]="field.name === 'name'">
                                        <mat-label>{{ i18n.t(field.label) }}</mat-label>
                                        <input
                                            matInput
                                            [formControlName]="field.name"
                                            [attr.data-testid]="'order-consignee-' + field.name"
                                        />
                                        @if (
                                            form.controls.consignee.controls[field.name].errors;
                                            as errors
                                        ) {
                                            <mat-error
                                                [attr.data-testid]="
                                                    'order-consignee-' + field.name + '-error'
                                                "
                                                >{{ fieldErrorText(errors) }}</mat-error
                                            >
                                        }
                                    </mat-form-field>
                                }
                            }
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">inventory_2</mat-icon>
                            <mat-card-title>{{ i18n.t('orders.cargo') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content class="fields">
                            <mat-form-field class="span-2">
                                <mat-label>{{ i18n.t('orders.cargoDescription') }}</mat-label>
                                <input
                                    matInput
                                    formControlName="cargoDescription"
                                    data-testid="order-cargoDescription"
                                />
                                @if (form.controls.cargoDescription.errors; as errors) {
                                    <mat-error data-testid="order-cargoDescription-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('orders.weightKg') }}</mat-label>
                                <input
                                    matInput
                                    type="number"
                                    formControlName="cargoWeightKg"
                                    data-testid="order-cargoWeightKg"
                                />
                                @if (form.controls.cargoWeightKg.errors; as errors) {
                                    <mat-error data-testid="order-cargoWeightKg-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field>
                                <mat-label>{{ i18n.t('common.loadMeters') }}</mat-label>
                                <!-- step="any" because the default step=1 makes a native number input
                                     reject a decimal like 8.4 - the same trap Phase 2 hit in the Vue form. -->
                                <input
                                    matInput
                                    type="number"
                                    step="any"
                                    formControlName="cargoLoadMeters"
                                    data-testid="order-cargoLoadMeters"
                                />
                                @if (form.controls.cargoLoadMeters.errors; as errors) {
                                    <mat-error data-testid="order-cargoLoadMeters-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                        </mat-card-content>
                    </mat-card>

                    <mat-card appearance="outlined">
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">schedule</mat-icon>
                            <mat-card-title>{{ i18n.t('orders.section.windows') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content>
                            <p class="window-label">{{ i18n.t('orders.pickupWindow') }}</p>
                            <div class="fields">
                                <mat-form-field>
                                    <mat-label>{{ i18n.t('common.from') }}</mat-label>
                                    <input
                                        matInput
                                        type="datetime-local"
                                        formControlName="pickupFrom"
                                        data-testid="order-pickupFrom"
                                    />
                                    @if (form.controls.pickupFrom.errors; as errors) {
                                        <mat-error data-testid="order-pickupFrom-error">{{
                                            fieldErrorText(errors)
                                        }}</mat-error>
                                    }
                                </mat-form-field>
                                <mat-form-field>
                                    <mat-label>{{ i18n.t('common.to') }}</mat-label>
                                    <input
                                        matInput
                                        type="datetime-local"
                                        formControlName="pickupTo"
                                        data-testid="order-pickupTo"
                                    />
                                    @if (form.controls.pickupTo.errors; as errors) {
                                        <mat-error data-testid="order-pickupTo-error">{{
                                            fieldErrorText(errors)
                                        }}</mat-error>
                                    }
                                </mat-form-field>
                            </div>
                            <p class="window-label">{{ i18n.t('orders.deliveryWindow') }}</p>
                            <div class="fields">
                                <mat-form-field>
                                    <mat-label>{{ i18n.t('common.from') }}</mat-label>
                                    <input
                                        matInput
                                        type="datetime-local"
                                        formControlName="deliveryFrom"
                                        data-testid="order-deliveryFrom"
                                    />
                                    @if (form.controls.deliveryFrom.errors; as errors) {
                                        <mat-error data-testid="order-deliveryFrom-error">{{
                                            fieldErrorText(errors)
                                        }}</mat-error>
                                    }
                                </mat-form-field>
                                <mat-form-field>
                                    <mat-label>{{ i18n.t('common.to') }}</mat-label>
                                    <input
                                        matInput
                                        type="datetime-local"
                                        formControlName="deliveryTo"
                                        data-testid="order-deliveryTo"
                                    />
                                    @if (form.controls.deliveryTo.errors; as errors) {
                                        <mat-error data-testid="order-deliveryTo-error">{{
                                            fieldErrorText(errors)
                                        }}</mat-error>
                                    }
                                </mat-form-field>
                            </div>
                        </mat-card-content>
                    </mat-card>

                    <mat-card
                        appearance="outlined"
                        class="full-width"
                        data-testid="order-special"
                    >
                        <mat-card-header>
                            <mat-icon mat-card-avatar class="avatar">notes</mat-icon>
                            <mat-card-title>{{ i18n.t('orders.special') }}</mat-card-title>
                        </mat-card-header>
                        <mat-card-content>
                            <mat-form-field class="block">
                                <mat-label>{{ i18n.t('orders.notes') }}</mat-label>
                                <textarea
                                    matInput
                                    rows="3"
                                    formControlName="notes"
                                    [maxlength]="notesMaxLength"
                                    data-testid="order-notes"
                                ></textarea>
                                <mat-hint align="end"
                                    >{{ form.controls.notes.value.length }} /
                                    {{ notesMaxLength }}</mat-hint
                                >
                                @if (form.controls.notes.errors; as errors) {
                                    <mat-error data-testid="order-notes-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <mat-form-field class="block">
                                <mat-label>{{ i18n.t('orders.confirmationNotes') }}</mat-label>
                                <textarea
                                    matInput
                                    rows="3"
                                    formControlName="confirmationNotes"
                                    [maxlength]="notesMaxLength"
                                    data-testid="order-confirmationNotes"
                                ></textarea>
                                <mat-hint align="end"
                                    >{{ form.controls.confirmationNotes.value.length }} /
                                    {{ notesMaxLength }}</mat-hint
                                >
                                @if (form.controls.confirmationNotes.errors; as errors) {
                                    <mat-error data-testid="order-confirmationNotes-error">{{
                                        fieldErrorText(errors)
                                    }}</mat-error>
                                }
                            </mat-form-field>
                            <div class="flags">
                                <mat-checkbox
                                    formControlName="requiresPartitionWall"
                                    data-testid="order-requiresPartitionWall"
                                    >{{ i18n.t('orders.requiresPartitionWall') }}</mat-checkbox
                                >
                                <mat-checkbox
                                    formControlName="requiresDoubleDeck"
                                    data-testid="order-requiresDoubleDeck"
                                    >{{ i18n.t('orders.requiresDoubleDeck') }}</mat-checkbox
                                >
                            </div>
                        </mat-card-content>
                    </mat-card>

                    @if (isEditMode) {
                        <mat-card appearance="outlined" class="full-width" data-testid="order-map">
                            <mat-card-header>
                                <mat-icon mat-card-avatar class="avatar">map</mat-icon>
                                <mat-card-title>{{ i18n.t('orders.map') }}</mat-card-title>
                            </mat-card-header>
                            <mat-card-content>
                                @if (locationError(); as message) {
                                    <p class="hint" data-testid="order-map-error">
                                        <mat-icon aria-hidden="true">info</mat-icon>{{ message }}
                                    </p>
                                }
                                @if (locations(); as located) {
                                    @if (mapMarkers().length) {
                                        <app-location-map [markers]="mapMarkers()" />
                                    }
                                    @if (!located.consignor) {
                                        <p class="hint" data-testid="order-map-consignor-missing">
                                            <mat-icon aria-hidden="true">info</mat-icon
                                            >{{ i18n.t('orders.map.consignorMissing') }}
                                        </p>
                                    }
                                    @if (!located.consignee) {
                                        <p class="hint" data-testid="order-map-consignee-missing">
                                            <mat-icon aria-hidden="true">info</mat-icon
                                            >{{ i18n.t('orders.map.consigneeMissing') }}
                                        </p>
                                    }
                                }
                            </mat-card-content>
                        </mat-card>

                        <mat-card
                            appearance="outlined"
                            class="full-width"
                            data-testid="order-tolls"
                        >
                            <mat-card-header>
                                <mat-icon mat-card-avatar class="avatar">toll</mat-icon>
                                <mat-card-title>{{ i18n.t('orders.tolls.title') }}</mat-card-title>
                                @if (session.can('dispatch.write')) {
                                    <button
                                        mat-stroked-button
                                        type="button"
                                        class="card-action"
                                        data-testid="order-toll-add"
                                        (click)="openTollDialog(null)"
                                    >
                                        <mat-icon>add</mat-icon>
                                        {{ i18n.t('orders.tolls.add') }}
                                    </button>
                                }
                            </mat-card-header>
                            <mat-card-content>
                                @if (tollError(); as message) {
                                    <p class="error-banner" data-testid="order-tolls-error">
                                        <mat-icon aria-hidden="true">error_outline</mat-icon
                                        >{{ message }}
                                    </p>
                                }
                                @if (tolls().length > 0) {
                                    <div class="table-scroll">
                                        <table mat-table [dataSource]="tolls()">
                                            <ng-container matColumnDef="category">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('orders.tolls.category') }}
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let t"
                                                    data-testid="order-toll-category"
                                                >
                                                    {{ tollCategoryName(t) }}
                                                </td>
                                                <td mat-footer-cell *matFooterCellDef>
                                                    {{ i18n.t('orders.sum') }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="emptyKm">
                                                <th mat-header-cell *matHeaderCellDef class="num">
                                                    {{ i18n.t('orders.tolls.emptyKm') }}
                                                </th>
                                                <td mat-cell *matCellDef="let t" class="num">
                                                    {{ t.emptyKm | number: '1.0-1' }}
                                                </td>
                                                <td mat-footer-cell *matFooterCellDef class="num">
                                                    {{ tollTotals().emptyKm | number: '1.0-1' }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="loadedKm">
                                                <th mat-header-cell *matHeaderCellDef class="num">
                                                    {{ i18n.t('orders.tolls.loadedKm') }}
                                                </th>
                                                <td mat-cell *matCellDef="let t" class="num">
                                                    {{ t.loadedKm | number: '1.0-1' }}
                                                </td>
                                                <td mat-footer-cell *matFooterCellDef class="num">
                                                    {{ tollTotals().loadedKm | number: '1.0-1' }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="factorPerKm">
                                                <th mat-header-cell *matHeaderCellDef class="num">
                                                    {{ i18n.t('orders.tolls.factorPerKm') }}
                                                </th>
                                                <td mat-cell *matCellDef="let t" class="num">
                                                    {{ t.factorPerKm | number: '1.2-4' }}
                                                </td>
                                                <td mat-footer-cell *matFooterCellDef></td>
                                            </ng-container>
                                            <ng-container matColumnDef="emptyAmount">
                                                <th mat-header-cell *matHeaderCellDef class="num">
                                                    {{ i18n.t('orders.tolls.emptyAmount') }}
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let t"
                                                    class="num"
                                                    data-testid="order-toll-emptyAmount"
                                                >
                                                    {{ t.emptyAmount | number: '1.2-2' }} €
                                                </td>
                                                <td
                                                    mat-footer-cell
                                                    *matFooterCellDef
                                                    class="num"
                                                    data-testid="order-toll-emptyAmount-sum"
                                                >
                                                    {{ tollTotals().emptyAmount | number: '1.2-2' }} €
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="loadedAmount">
                                                <th mat-header-cell *matHeaderCellDef class="num">
                                                    {{ i18n.t('orders.tolls.loadedAmount') }}
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let t"
                                                    class="num"
                                                    data-testid="order-toll-loadedAmount"
                                                >
                                                    {{ t.loadedAmount | number: '1.2-2' }} €
                                                </td>
                                                <td
                                                    mat-footer-cell
                                                    *matFooterCellDef
                                                    class="num"
                                                    data-testid="order-toll-loadedAmount-sum"
                                                >
                                                    {{ tollTotals().loadedAmount | number: '1.2-2' }} €
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="modified">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('orders.modified') }}
                                                </th>
                                                <td mat-cell *matCellDef="let t" class="modified">
                                                    {{ modifiedText(t) }}
                                                </td>
                                                <td mat-footer-cell *matFooterCellDef></td>
                                            </ng-container>
                                            <ng-container matColumnDef="actions">
                                                <th
                                                    mat-header-cell
                                                    *matHeaderCellDef
                                                    class="actions-cell"
                                                >
                                                    <span class="cdk-visually-hidden">{{
                                                        i18n.t('common.actions')
                                                    }}</span>
                                                </th>
                                                <td mat-cell *matCellDef="let t" class="actions-cell">
                                                    <button
                                                        mat-icon-button
                                                        type="button"
                                                        data-testid="order-toll-edit"
                                                        [matTooltip]="i18n.t('common.edit')"
                                                        [attr.aria-label]="
                                                            i18n.t('common.edit') +
                                                            ' ' +
                                                            tollCategoryName(t)
                                                        "
                                                        (click)="openTollDialog(t)"
                                                    >
                                                        <mat-icon>edit</mat-icon>
                                                    </button>
                                                    <button
                                                        mat-icon-button
                                                        type="button"
                                                        class="delete"
                                                        data-testid="order-toll-delete"
                                                        [matTooltip]="i18n.t('common.delete')"
                                                        [attr.aria-label]="
                                                            i18n.t('common.delete') +
                                                            ' ' +
                                                            tollCategoryName(t)
                                                        "
                                                        (click)="deleteToll(t)"
                                                    >
                                                        <mat-icon>delete</mat-icon>
                                                    </button>
                                                </td>
                                                <td mat-footer-cell *matFooterCellDef></td>
                                            </ng-container>
                                            <tr
                                                mat-header-row
                                                *matHeaderRowDef="tollColumns()"
                                            ></tr>
                                            <tr
                                                mat-row
                                                *matRowDef="let row; columns: tollColumns()"
                                                data-testid="order-toll-row"
                                            ></tr>
                                            <tr
                                                mat-footer-row
                                                *matFooterRowDef="tollColumns()"
                                                class="sum-row"
                                            ></tr>
                                        </table>
                                    </div>
                                } @else if (tollsLoaded()) {
                                    <p class="hint" data-testid="order-tolls-empty">
                                        <mat-icon aria-hidden="true">toll</mat-icon>
                                        {{ i18n.t('orders.tolls.empty') }}
                                    </p>
                                }
                            </mat-card-content>
                        </mat-card>

                        <mat-card
                            appearance="outlined"
                            class="full-width"
                            data-testid="order-charges"
                        >
                            <mat-card-header>
                                <mat-icon mat-card-avatar class="avatar">post_add</mat-icon>
                                <mat-card-title>{{
                                    i18n.t('orders.charges.title')
                                }}</mat-card-title>
                                @if (session.can('dispatch.write')) {
                                    <button
                                        mat-stroked-button
                                        type="button"
                                        class="card-action"
                                        data-testid="order-charge-add"
                                        (click)="openChargeDialog(null)"
                                    >
                                        <mat-icon>add</mat-icon>
                                        {{ i18n.t('orders.charges.add') }}
                                    </button>
                                }
                            </mat-card-header>
                            <mat-card-content>
                                @if (chargeError(); as message) {
                                    <p class="error-banner" data-testid="order-charges-error">
                                        <mat-icon aria-hidden="true">error_outline</mat-icon
                                        >{{ message }}
                                    </p>
                                }
                                @if (charges().length > 0) {
                                    <div class="table-scroll">
                                        <table mat-table [dataSource]="charges()">
                                            <ng-container matColumnDef="description">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('orders.charges.description') }}
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let c"
                                                    data-testid="order-charge-description"
                                                >
                                                    {{ c.description }}
                                                </td>
                                                <td mat-footer-cell *matFooterCellDef>
                                                    {{ i18n.t('orders.sum') }}
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="quantity">
                                                <th mat-header-cell *matHeaderCellDef class="num">
                                                    {{ i18n.t('orders.charges.quantity') }}
                                                </th>
                                                <td mat-cell *matCellDef="let c" class="num">
                                                    {{ c.quantity | number: '1.0-2' }}
                                                </td>
                                                <td mat-footer-cell *matFooterCellDef></td>
                                            </ng-container>
                                            <ng-container matColumnDef="unit">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('orders.charges.unit') }}
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let c"
                                                    data-testid="order-charge-unit"
                                                >
                                                    {{ unitName(c) }}
                                                </td>
                                                <td mat-footer-cell *matFooterCellDef></td>
                                            </ng-container>
                                            <ng-container matColumnDef="unitPrice">
                                                <th mat-header-cell *matHeaderCellDef class="num">
                                                    {{ i18n.t('orders.charges.unitPrice') }}
                                                </th>
                                                <td mat-cell *matCellDef="let c" class="num">
                                                    {{ c.unitPrice | number: '1.2-2' }} €
                                                </td>
                                                <td mat-footer-cell *matFooterCellDef></td>
                                            </ng-container>
                                            <ng-container matColumnDef="total">
                                                <th mat-header-cell *matHeaderCellDef class="num">
                                                    {{ i18n.t('orders.charges.total') }}
                                                </th>
                                                <td
                                                    mat-cell
                                                    *matCellDef="let c"
                                                    class="num"
                                                    data-testid="order-charge-total"
                                                >
                                                    {{ c.total | number: '1.2-2' }} €
                                                </td>
                                                <td
                                                    mat-footer-cell
                                                    *matFooterCellDef
                                                    class="num"
                                                    data-testid="order-charge-total-sum"
                                                >
                                                    {{ chargeTotal() | number: '1.2-2' }} €
                                                </td>
                                            </ng-container>
                                            <ng-container matColumnDef="modified">
                                                <th mat-header-cell *matHeaderCellDef>
                                                    {{ i18n.t('orders.modified') }}
                                                </th>
                                                <td mat-cell *matCellDef="let c" class="modified">
                                                    {{ modifiedText(c) }}
                                                </td>
                                                <td mat-footer-cell *matFooterCellDef></td>
                                            </ng-container>
                                            <ng-container matColumnDef="actions">
                                                <th
                                                    mat-header-cell
                                                    *matHeaderCellDef
                                                    class="actions-cell"
                                                >
                                                    <span class="cdk-visually-hidden">{{
                                                        i18n.t('common.actions')
                                                    }}</span>
                                                </th>
                                                <td mat-cell *matCellDef="let c" class="actions-cell">
                                                    <button
                                                        mat-icon-button
                                                        type="button"
                                                        data-testid="order-charge-edit"
                                                        [matTooltip]="i18n.t('common.edit')"
                                                        [attr.aria-label]="
                                                            i18n.t('common.edit') +
                                                            ' ' +
                                                            c.description
                                                        "
                                                        (click)="openChargeDialog(c)"
                                                    >
                                                        <mat-icon>edit</mat-icon>
                                                    </button>
                                                    <button
                                                        mat-icon-button
                                                        type="button"
                                                        class="delete"
                                                        data-testid="order-charge-delete"
                                                        [matTooltip]="i18n.t('common.delete')"
                                                        [attr.aria-label]="
                                                            i18n.t('common.delete') +
                                                            ' ' +
                                                            c.description
                                                        "
                                                        (click)="deleteCharge(c)"
                                                    >
                                                        <mat-icon>delete</mat-icon>
                                                    </button>
                                                </td>
                                                <td mat-footer-cell *matFooterCellDef></td>
                                            </ng-container>
                                            <tr
                                                mat-header-row
                                                *matHeaderRowDef="chargeColumns()"
                                            ></tr>
                                            <tr
                                                mat-row
                                                *matRowDef="let row; columns: chargeColumns()"
                                                data-testid="order-charge-row"
                                            ></tr>
                                            <tr
                                                mat-footer-row
                                                *matFooterRowDef="chargeColumns()"
                                                class="sum-row"
                                            ></tr>
                                        </table>
                                    </div>
                                } @else if (chargesLoaded()) {
                                    <p class="hint" data-testid="order-charges-empty">
                                        <mat-icon aria-hidden="true">post_add</mat-icon>
                                        {{ i18n.t('orders.charges.empty') }}
                                    </p>
                                }
                            </mat-card-content>
                        </mat-card>
                    }
                </div>

                <div class="action-bar">
                    <button
                        mat-button
                        type="button"
                        data-testid="order-cancel-edit"
                        (click)="cancel()"
                    >
                        {{ i18n.t('common.cancel') }}
                    </button>
                    <button mat-flat-button type="submit" data-testid="order-save">
                        <mat-icon>save</mat-icon>
                        {{ i18n.t('common.save') }}
                    </button>
                </div>
            </form>
        </div>
    `,
    styles: [
        `
            .span-2 {
                grid-column: 1 / -1;
            }

            .window-label {
                margin: 0 0 8px;
                color: var(--mat-sys-on-surface-variant);
                font: var(--mat-sys-title-small);
            }

            mat-form-field.block {
                display: block;
                width: 100%;
            }

            .flags {
                display: flex;
                flex-wrap: wrap;
                gap: 8px 24px;
                margin-top: 4px;
            }

            .num {
                text-align: right;
            }

            td.num {
                font-variant-numeric: tabular-nums;
            }

            .modified {
                color: var(--mat-sys-on-surface-variant);
                white-space: nowrap;
            }

            .sum-row td {
                font-weight: 600;
            }
        `,
    ],
})
export class OrderFormComponent {
    private readonly service = inject(OrderService);
    private readonly route = inject(ActivatedRoute);
    private readonly router = inject(Router);
    private readonly fb = inject(FormBuilder);
    private readonly dialog = inject(MatDialog);
    private readonly catalogService = inject(VehicleCatalogService);
    private readonly locationService = inject(CustomerLocationService);
    // SessionService.ready is what guarantees angular-auth-oidc-client has rehydrated its
    // stored session before the first request goes out. A form reached by a DIRECT navigation -
    // a bookmark, a page reload while editing, browser back - has no list component in its
    // lifetime, so without this every request from it answered 401 to a plainly signed-in user.
    // The one checkAuth() behind it runs in the App component.
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    protected readonly addressFields = [
        { name: 'name', label: 'common.name' },
        { name: 'street', label: 'common.street' },
        { name: 'postalCode', label: 'common.postalCode' },
        { name: 'city', label: 'common.city' },
        { name: 'country', label: 'common.country' },
    ] as const;

    protected readonly orderId = this.route.snapshot.paramMap.get('id');
    protected readonly isEditMode = this.orderId !== null;
    protected readonly notesMaxLength = NOTES_MAX_LENGTH;
    protected readonly formError = signal<string | null>(null);
    protected readonly locations = signal<OrderLocations | null>(null);
    protected readonly locationError = signal<string | null>(null);
    protected readonly locationOptions = signal<CustomerLocationOption[]>([]);
    protected readonly locationPickerError = signal<string | null>(null);

    protected readonly tolls = signal<OrderToll[]>([]);
    protected readonly tollsLoaded = signal(false);
    protected readonly tollError = signal<string | null>(null);
    protected readonly charges = signal<OrderCharge[]>([]);
    protected readonly chargesLoaded = signal(false);
    protected readonly chargeError = signal<string | null>(null);
    protected readonly tollCategories = signal<VehicleCatalogEntry[]>([]);
    protected readonly units = signal<VehicleCatalogEntry[]>([]);
    private readonly tollCategoryIndex = computed(() => catalogIndex(this.tollCategories()));
    private readonly unitIndex = computed(() => catalogIndex(this.units()));
    protected readonly tollColumns = computed(() =>
        this.session.can('dispatch.write') ? [...TOLL_COLUMNS, 'actions'] : [...TOLL_COLUMNS],
    );
    protected readonly chargeColumns = computed(() =>
        this.session.can('dispatch.write') ? [...CHARGE_COLUMNS, 'actions'] : [...CHARGE_COLUMNS],
    );
    // The sums mirror the footer boxes of the old software's toll and charge grids.
    protected readonly tollTotals = computed(() =>
        this.tolls().reduce(
            (sums, toll) => ({
                emptyKm: sums.emptyKm + toll.emptyKm,
                loadedKm: sums.loadedKm + toll.loadedKm,
                emptyAmount: sums.emptyAmount + toll.emptyAmount,
                loadedAmount: sums.loadedAmount + toll.loadedAmount,
            }),
            { emptyKm: 0, loadedKm: 0, emptyAmount: 0, loadedAmount: 0 },
        ),
    );
    protected readonly chargeTotal = computed(() =>
        this.charges().reduce((sum, charge) => sum + charge.total, 0),
    );
    protected readonly mapMarkers = computed<MapMarker[]>(() => {
        const located = this.locations();
        if (!located) {
            return [];
        }
        return [
            this.marker(located.consignor, 'A', this.i18n.t('orders.consignor')),
            this.marker(located.consignee, 'B', this.i18n.t('orders.consignee')),
        ].filter((marker): marker is MapMarker => marker !== null);
    });

    protected readonly form = this.fb.nonNullable.group({
        consignor: this.addressGroup(),
        consignee: this.addressGroup(),
        cargoDescription: ['', Validators.required],
        cargoWeightKg: [0, [Validators.required, Validators.min(1)]],
        cargoLoadMeters: [0, [Validators.required, Validators.min(0.01)]],
        pickupFrom: ['', Validators.required],
        pickupTo: ['', Validators.required],
        deliveryFrom: ['', Validators.required],
        deliveryTo: ['', Validators.required],
        consigneeLocationId: [''],
        notes: ['', Validators.maxLength(NOTES_MAX_LENGTH)],
        confirmationNotes: ['', Validators.maxLength(NOTES_MAX_LENGTH)],
        requiresPartitionWall: [false],
        requiresDoubleDeck: [false],
    });

    constructor() {
        this.loadLocationOptions();
        if (this.isEditMode) {
            this.session.ready
                .pipe(switchMap(() => this.service.getById(this.orderId!)))
                .subscribe({
                    next: (order: Order) => this.patchFrom(order),
                    // The load path gets its own message: "could not be saved" on a failed
                    // getById would be plainly wrong.
                    error: (error: HttpErrorResponse) =>
                        this.formError.set(
                            this.describeFailure(error, this.i18n.t('orders.error.load')),
                        ),
                });
            this.loadLocations();
            // Toll categories and units resolve codes to display names in the two grids; the
            // full entry lists (including inactive ones) also feed the dialogs' selects.
            this.session.ready
                .pipe(
                    switchMap(() =>
                        forkJoin([
                            this.catalogService.list('TollCategory'),
                            this.catalogService.list('Unit'),
                        ]),
                    ),
                )
                .subscribe({
                    next: ([tollCategories, units]) => {
                        this.tollCategories.set(tollCategories);
                        this.units.set(units);
                    },
                    error: (error: HttpErrorResponse) =>
                        this.formError.set(
                            `${this.i18n.t('orders.error.catalogs')} (HTTP ${error.status})`,
                        ),
                });
            this.loadTolls();
            this.loadCharges();
        }
    }

    // Picking a location only copies its address into the consignee fields. The order keeps
    // that copy and the reference, so later edits to the location do not change this order.
    protected onLocationChange(event: Event): void {
        const id = (event.target as HTMLSelectElement).value;
        const option = this.locationOptions().find((o) => o.id === id);
        if (!option) {
            return;
        }
        this.form.controls.consignee.patchValue({
            name: option.name,
            street: option.street,
            postalCode: option.postalCode,
            city: option.city,
            country: option.country,
        });
    }

    private loadLocationOptions(): void {
        this.session.ready.pipe(switchMap(() => this.locationService.listAll())).subscribe({
            next: (page) => this.locationOptions.set(page.items),
            error: (error: HttpErrorResponse) =>
                this.locationPickerError.set(
                    this.describeFailure(error, this.i18n.t('orders.consigneeLocation.error')),
                ),
        });
    }

    // The map is advisory: a failure here must never block the form, so it has its own error.
    // 503 means routing/geocoding is not configured or unreachable, which deserves its own wording.
    private loadLocations(): void {
        this.session.ready
            .pipe(switchMap(() => this.service.getLocations(this.orderId!)))
            .subscribe({
                next: (located) => this.locations.set(located),
                error: (error: HttpErrorResponse) =>
                    this.locationError.set(
                        error.status === 503
                            ? this.i18n.t('map.unavailable')
                            : this.describeFailure(error, this.i18n.t('map.error.load')),
                    ),
            });
    }

    private marker(party: LocatedParty | null, label: string, role: string): MapMarker | null {
        if (!party) {
            return null;
        }
        return {
            latitude: party.latitude,
            longitude: party.longitude,
            label,
            title: `${role}: ${party.name}, ${party.address}`,
        };
    }

    // See DriverFormComponent.fieldErrorText for the full reasoning.
    protected fieldErrorText(errors: ValidationErrors): string {
        if (errors['server']) {
            return errors['server'] as string;
        }
        if (errors['required']) {
            return this.i18n.t('common.fieldRequired');
        }
        if (errors['min']) {
            return this.i18n.t('common.fieldPositive');
        }
        return this.i18n.t('common.fieldInvalid');
    }

    protected save(): void {
        this.formError.set(null);
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        const value = this.form.getRawValue();
        const request: OrderWriteRequest = {
            consignor: value.consignor,
            consignee: value.consignee,
            cargoDescription: value.cargoDescription,
            cargoWeightKg: Number(value.cargoWeightKg),
            cargoLoadMeters: Number(value.cargoLoadMeters),
            pickupFrom: this.toIso(value.pickupFrom),
            pickupTo: this.toIso(value.pickupTo),
            deliveryFrom: this.toIso(value.deliveryFrom),
            deliveryTo: this.toIso(value.deliveryTo),
            consigneeLocationId: value.consigneeLocationId || null,
            notes: value.notes.trim() === '' ? null : value.notes,
            confirmationNotes: value.confirmationNotes.trim() === '' ? null : value.confirmationNotes,
            requiresPartitionWall: value.requiresPartitionWall,
            requiresDoubleDeck: value.requiresDoubleDeck,
        };

        const save$: Observable<Order> = this.session.ready.pipe(
            switchMap(() =>
                this.isEditMode
                    ? this.service.update(this.orderId!, request)
                    : this.service.create(request),
            ),
        );

        save$.subscribe({
            next: () => this.router.navigateByUrl('/orders'),
            error: (error: HttpErrorResponse) => this.applyServerErrors(error),
        });
    }

    protected cancel(): void {
        this.router.navigateByUrl('/orders');
    }

    private addressGroup() {
        return this.fb.nonNullable.group({
            name: ['', Validators.required],
            street: ['', Validators.required],
            postalCode: ['', Validators.required],
            city: ['', Validators.required],
            country: ['DE', Validators.required],
        });
    }

    private patchFrom(order: Order): void {
        this.form.patchValue({
            consignor: order.consignor,
            consignee: order.consignee,
            cargoDescription: order.cargoDescription,
            cargoWeightKg: order.cargoWeightKg,
            cargoLoadMeters: order.cargoLoadMeters,
            pickupFrom: this.toLocalInput(order.pickupFrom),
            pickupTo: this.toLocalInput(order.pickupTo),
            deliveryFrom: this.toLocalInput(order.deliveryFrom),
            deliveryTo: this.toLocalInput(order.deliveryTo),
            consigneeLocationId: order.consigneeLocationId ?? '',
            notes: order.notes ?? '',
            confirmationNotes: order.confirmationNotes ?? '',
            requiresPartitionWall: order.requiresPartitionWall,
            requiresDoubleDeck: order.requiresDoubleDeck,
        });
    }

    // A datetime-local input has no zone, so its value is the user's wall-clock time. Sending
    // it verbatim would let the API read it as UTC and shift the window by the local offset;
    // constructing a Date first makes the browser attach the local zone before serialising.
    private toIso(localValue: string): string {
        return new Date(localValue).toISOString();
    }

    // The inverse: an ISO instant rendered back into the local wall-clock string the input
    // expects ("YYYY-MM-DDTHH:mm"), so a round-trip through the edit form is lossless.
    private toLocalInput(iso: string): string {
        const date = new Date(iso);
        const offsetMinutes = date.getTimezoneOffset();
        const local = new Date(date.getTime() - offsetMinutes * 60_000);
        return local.toISOString().slice(0, 16);
    }

    // See DriverFormComponent.applyServerErrors for why binding ProblemDetails' `errors`
    // dictionary is correct here (it supersedes a Phase 1 instruction that forbade it).
    // Order keys can be DOTTED: the validator's nested rules produce "Consignor.Name" and
    // "Consignee.City" alongside flat keys like "CargoWeightKg", so every dotted segment is
    // lowercased individually and the result used as an Angular control path. A key that
    // names no control still reaches the user, as a form-level message.
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

        this.formError.set(this.describeFailure(error, this.i18n.t('orders.error.save')));
    }

    // See DriverFormComponent.describeFailure: `fallback` is supplied per call site (loading
    // vs. saving) because a shared string is wrong for at least one of them.
    private describeFailure(error: HttpErrorResponse, fallback: string): string {
        const problem = error.error as ProblemDetailsBody | null;
        const message = problem?.detail ?? problem?.title ?? fallback;
        return `${message} (HTTP ${error.status})`;
    }

    protected tollCategoryName(toll: OrderToll): string {
        return catalogName(this.tollCategoryIndex(), toll.tollCategory) ?? toll.tollCategory;
    }

    protected unitName(charge: OrderCharge): string {
        return catalogName(this.unitIndex(), charge.unit) ?? '';
    }

    protected modifiedText(entry: OrderToll | OrderCharge): string {
        return this.i18n.t('orders.modifiedValue', {
            date: formatDateTime(entry.modifiedAt, this.i18n.locale()),
            user: entry.modifiedBy ?? '–',
        });
    }

    // Tolls and charges are saved on their own endpoints right from the dialog, independent of
    // the order's Save button - unsaved edits in the form are neither sent nor lost.
    protected openTollDialog(toll: OrderToll | null): void {
        this.tollError.set(null);
        this.dialog
            .open<OrderTollDialogComponent, OrderTollDialogData, OrderToll>(
                OrderTollDialogComponent,
                {
                    data: {
                        orderId: this.orderId!,
                        toll,
                        tollCategories: this.tollCategories(),
                    },
                    width: '520px',
                    autoFocus: 'first-tabbable',
                },
            )
            .afterClosed()
            .pipe(filter((saved) => !!saved))
            .subscribe(() => this.loadTolls());
    }

    protected deleteToll(toll: OrderToll): void {
        this.tollError.set(null);
        const data: ConfirmDialogData = {
            title: this.i18n.t('orders.tolls.deleteTitle'),
            message: this.i18n.t('orders.tolls.deleteConfirm', {
                category: this.tollCategoryName(toll),
            }),
            confirmLabel: this.i18n.t('common.delete'),
            cancelLabel: this.i18n.t('common.cancel'),
            confirmTestId: 'order-toll-delete-confirm',
            cancelTestId: 'order-toll-delete-cancel',
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
                switchMap(() => this.session.ready),
                switchMap(() => this.service.removeToll(this.orderId!, toll.id)),
            )
            .subscribe({
                next: () => this.loadTolls(),
                error: (error: HttpErrorResponse) =>
                    this.tollError.set(
                        this.describeFailure(error, this.i18n.t('orders.tolls.error.delete')),
                    ),
            });
    }

    protected openChargeDialog(charge: OrderCharge | null): void {
        this.chargeError.set(null);
        this.dialog
            .open<OrderChargeDialogComponent, OrderChargeDialogData, OrderCharge>(
                OrderChargeDialogComponent,
                {
                    data: {
                        orderId: this.orderId!,
                        charge,
                        units: this.units(),
                    },
                    width: '520px',
                    autoFocus: 'first-tabbable',
                },
            )
            .afterClosed()
            .pipe(filter((saved) => !!saved))
            .subscribe(() => this.loadCharges());
    }

    protected deleteCharge(charge: OrderCharge): void {
        this.chargeError.set(null);
        const data: ConfirmDialogData = {
            title: this.i18n.t('orders.charges.deleteTitle'),
            message: this.i18n.t('orders.charges.deleteConfirm', {
                description: charge.description,
            }),
            confirmLabel: this.i18n.t('common.delete'),
            cancelLabel: this.i18n.t('common.cancel'),
            confirmTestId: 'order-charge-delete-confirm',
            cancelTestId: 'order-charge-delete-cancel',
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
                switchMap(() => this.session.ready),
                switchMap(() => this.service.removeCharge(this.orderId!, charge.id)),
            )
            .subscribe({
                next: () => this.loadCharges(),
                error: (error: HttpErrorResponse) =>
                    this.chargeError.set(
                        this.describeFailure(error, this.i18n.t('orders.charges.error.delete')),
                    ),
            });
    }

    private loadTolls(): void {
        this.session.ready
            .pipe(switchMap(() => this.service.listTolls(this.orderId!)))
            .subscribe({
                next: (tolls) => {
                    this.tolls.set(tolls);
                    this.tollsLoaded.set(true);
                },
                error: (error: HttpErrorResponse) =>
                    this.tollError.set(
                        this.describeFailure(error, this.i18n.t('orders.tolls.error.list')),
                    ),
            });
    }

    private loadCharges(): void {
        this.session.ready
            .pipe(switchMap(() => this.service.listCharges(this.orderId!)))
            .subscribe({
                next: (charges) => {
                    this.charges.set(charges);
                    this.chargesLoaded.set(true);
                },
                error: (error: HttpErrorResponse) =>
                    this.chargeError.set(
                        this.describeFailure(error, this.i18n.t('orders.charges.error.list')),
                    ),
            });
    }
}
