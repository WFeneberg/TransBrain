import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal, viewChild } from '@angular/core';
import { StepperSelectionEvent } from '@angular/cdk/stepper';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatStepper, MatStepperModule } from '@angular/material/stepper';
import { RouterLink } from '@angular/router';
import { SessionService } from '../auth/session.service';
import { I18nService, TranslationKey } from '../i18n/i18n.service';
import {
    IMPORT_FILE_EXTENSIONS,
    IMPORT_MAX_FILE_BYTES,
    ImportDatabaseProvider,
    ImportExecution,
    ImportFieldKind,
    ImportMappingRequest,
    ImportPreview,
    ImportService,
    ImportSession,
    ImportTable,
    ImportTarget,
    ImportTargetField,
} from './import.service';

/** How many rows the preview step requests - enough to judge the mapping, small enough to scan. */
const PREVIEW_ROWS = 20;

const MAX_FILE_MB = IMPORT_MAX_FILE_BYTES / (1024 * 1024);

/** Database providers for live connections; Access and SQLite files go through the file upload. */
const PROVIDERS: readonly { value: ImportDatabaseProvider; label: string }[] = [
    { value: 'PostgreSql', label: 'PostgreSQL' },
    { value: 'SqlServer', label: 'SQL Server' },
    { value: 'MySql', label: 'MySQL' },
    { value: 'Odbc', label: 'ODBC' },
];

const KIND_KEYS: Record<ImportFieldKind, TranslationKey> = {
    Text: 'imports.kind.Text',
    Integer: 'imports.kind.Integer',
    Decimal: 'imports.kind.Decimal',
    Date: 'imports.kind.Date',
    Boolean: 'imports.kind.Boolean',
    TextList: 'imports.kind.TextList',
};

/** One editable line of the mapping step: a target field and what should fill it. */
interface MappingRow {
    field: ImportTargetField;
    sourceColumn: string | null;
    constantValue: string;
}

/**
 * Five-step wizard that walks an administrator from a source (file upload or live database)
 * through table/target selection, field mapping and a validated preview to the actual import.
 * Every row is created through the same commands and validation as manual entry.
 */
@Component({
    selector: 'app-import-wizard',
    standalone: true,
    imports: [
        MatButtonModule,
        MatButtonToggleModule,
        MatChipsModule,
        MatFormFieldModule,
        MatIconModule,
        MatInputModule,
        MatProgressBarModule,
        MatProgressSpinnerModule,
        MatSelectModule,
        MatStepperModule,
        RouterLink,
    ],
    styleUrl: '../shared/page-layout.scss',
    template: `
        @if (session.isAuthenticated()) {
            <div class="page">
                <header class="page-header">
                    <div>
                        <h1>{{ i18n.t('imports.title') }}</h1>
                        <p class="subtitle">{{ i18n.t('imports.subtitle') }}</p>
                    </div>
                    <div class="header-actions">
                        <a
                            mat-stroked-button
                            routerLink="/import/verlauf"
                            data-testid="import-history-link"
                        >
                            <mat-icon>history</mat-icon>
                            {{ i18n.t('imports.historyLink') }}
                        </a>
                    </div>
                </header>

                <section class="wizard-card">
                    <mat-stepper linear #stepper (selectionChange)="onStepChange($event)">
                        <!-- Schritt 1: Quelle -->
                        <mat-step [completed]="!!importSession()" [editable]="wizardEditable()">
                            <ng-template matStepLabel>{{
                                i18n.t('imports.step.source')
                            }}</ng-template>
                            <div class="step-body">
                                <mat-button-toggle-group
                                    [value]="sourceMode()"
                                    (change)="sourceMode.set($event.value)"
                                    data-testid="import-source-mode"
                                >
                                    <mat-button-toggle value="file" data-testid="import-mode-file">
                                        <mat-icon>upload_file</mat-icon>
                                        {{ i18n.t('imports.source.file') }}
                                    </mat-button-toggle>
                                    <mat-button-toggle
                                        value="database"
                                        data-testid="import-mode-database"
                                    >
                                        <mat-icon>database</mat-icon>
                                        {{ i18n.t('imports.source.database') }}
                                    </mat-button-toggle>
                                </mat-button-toggle-group>

                                @if (sourceMode() === 'file') {
                                    <div class="file-row">
                                        <input
                                            type="file"
                                            hidden
                                            #fileInput
                                            [accept]="acceptAttribute"
                                            (change)="onFileSelected($event)"
                                            data-testid="import-file-input"
                                        />
                                        <button
                                            mat-stroked-button
                                            type="button"
                                            (click)="fileInput.click()"
                                            data-testid="import-choose-file"
                                        >
                                            <mat-icon>attach_file</mat-icon>
                                            {{ i18n.t('imports.source.chooseFile') }}
                                        </button>
                                        <span class="file-name" data-testid="import-file-name">
                                            {{
                                                selectedFile()?.name ??
                                                    i18n.t('imports.source.noFile')
                                            }}
                                        </span>
                                    </div>
                                    <p class="hint">
                                        <mat-icon aria-hidden="true">info</mat-icon>
                                        {{
                                            i18n.t('imports.source.fileHint', {
                                                extensions: allowedExtensions,
                                                maxMb: maxFileMb,
                                            })
                                        }}
                                    </p>
                                } @else {
                                    <div class="database-form">
                                        <mat-form-field
                                            appearance="outline"
                                            subscriptSizing="dynamic"
                                        >
                                            <mat-label>{{
                                                i18n.t('imports.source.provider')
                                            }}</mat-label>
                                            <mat-select
                                                [value]="provider()"
                                                (selectionChange)="provider.set($event.value)"
                                                data-testid="import-provider"
                                            >
                                                @for (option of providers; track option.value) {
                                                    <mat-option [value]="option.value">{{
                                                        option.label
                                                    }}</mat-option>
                                                }
                                            </mat-select>
                                        </mat-form-field>
                                        <mat-form-field
                                            appearance="outline"
                                            subscriptSizing="dynamic"
                                            class="grow"
                                        >
                                            <mat-label>{{
                                                i18n.t('imports.source.connectionString')
                                            }}</mat-label>
                                            <input
                                                matInput
                                                [value]="connectionString()"
                                                (input)="
                                                    connectionString.set($any($event.target).value)
                                                "
                                                placeholder="Host=localhost;Database=altsystem;Username=...;Password=..."
                                                data-testid="import-connection-string"
                                            />
                                        </mat-form-field>
                                    </div>
                                    <p class="hint">
                                        <mat-icon aria-hidden="true">info</mat-icon>
                                        {{ i18n.t('imports.source.connectionStringHint') }}
                                        {{ i18n.t('imports.source.accessHint') }}
                                    </p>
                                }

                                @if (sourceError(); as message) {
                                    <p class="error-banner" data-testid="import-source-error">
                                        <mat-icon aria-hidden="true">error_outline</mat-icon
                                        >{{ message }}
                                    </p>
                                }
                                @if (creating()) {
                                    <mat-progress-bar mode="indeterminate" />
                                }
                            </div>
                            <div class="step-actions">
                                @if (sourceMode() === 'file') {
                                    <button
                                        mat-flat-button
                                        type="button"
                                        [disabled]="!selectedFile() || creating()"
                                        (click)="uploadFile()"
                                        data-testid="import-analyze"
                                    >
                                        <mat-icon>cloud_upload</mat-icon>
                                        {{ i18n.t('imports.source.upload') }}
                                    </button>
                                } @else {
                                    <button
                                        mat-flat-button
                                        type="button"
                                        [disabled]="!connectionString().trim() || creating()"
                                        (click)="connectDatabase()"
                                        data-testid="import-connect"
                                    >
                                        <mat-icon>cable</mat-icon>
                                        {{ i18n.t('imports.source.connect') }}
                                    </button>
                                }
                            </div>
                        </mat-step>

                        <!-- Schritt 2: Tabelle & Ziel -->
                        <mat-step
                            [completed]="!!selectedTable() && !!selectedTarget()"
                            [editable]="wizardEditable()"
                        >
                            <ng-template matStepLabel>{{
                                i18n.t('imports.step.tableTarget')
                            }}</ng-template>
                            <div class="step-body">
                                <div class="database-form">
                                    <mat-form-field
                                        appearance="outline"
                                        subscriptSizing="dynamic"
                                        class="grow"
                                    >
                                        <mat-label>{{ i18n.t('imports.table.label') }}</mat-label>
                                        <mat-select
                                            [value]="selectedTableName()"
                                            (selectionChange)="selectedTableName.set($event.value)"
                                            data-testid="import-table-select"
                                        >
                                            @for (table of tables(); track table.name) {
                                                <mat-option [value]="table.name">
                                                    {{ table.name }}
                                                    ({{
                                                        i18n.t('imports.table.rowCount', {
                                                            count: table.rowCount,
                                                        })
                                                    }})
                                                </mat-option>
                                            }
                                        </mat-select>
                                    </mat-form-field>
                                    <mat-form-field
                                        appearance="outline"
                                        subscriptSizing="dynamic"
                                        class="grow"
                                    >
                                        <mat-label>{{ i18n.t('imports.target.label') }}</mat-label>
                                        <mat-select
                                            [value]="selectedEntity()"
                                            (selectionChange)="selectedEntity.set($event.value)"
                                            data-testid="import-target-select"
                                        >
                                            @for (target of targets(); track target.entity) {
                                                <mat-option [value]="target.entity">{{
                                                    target.label
                                                }}</mat-option>
                                            }
                                        </mat-select>
                                    </mat-form-field>
                                </div>

                                @if (targetsError(); as message) {
                                    <p class="error-banner" data-testid="import-targets-error">
                                        <mat-icon aria-hidden="true">error_outline</mat-icon
                                        >{{ message }}
                                    </p>
                                }

                                @if (selectedTable(); as table) {
                                    <h3 class="section-title">
                                        {{ i18n.t('imports.table.sample') }}
                                    </h3>
                                    <div class="table-card">
                                        <div class="table-scroll">
                                            <table
                                                class="plain-table"
                                                data-testid="import-sample-table"
                                            >
                                                <thead>
                                                    <tr>
                                                        @for (
                                                            column of table.columns;
                                                            track column
                                                        ) {
                                                            <th>{{ column }}</th>
                                                        }
                                                    </tr>
                                                </thead>
                                                <tbody>
                                                    @for (row of table.sampleRows; track $index) {
                                                        <tr>
                                                            @for (cell of row; track $index) {
                                                                <td>{{ cell ?? '–' }}</td>
                                                            }
                                                        </tr>
                                                    }
                                                </tbody>
                                            </table>
                                        </div>
                                    </div>
                                }
                            </div>
                            <div class="step-actions">
                                <button mat-button type="button" matStepperPrevious>
                                    {{ i18n.t('imports.back') }}
                                </button>
                                <button
                                    mat-flat-button
                                    type="button"
                                    [disabled]="!selectedTable() || !selectedTarget()"
                                    matStepperNext
                                    data-testid="import-to-mapping"
                                >
                                    {{ i18n.t('imports.next') }}
                                </button>
                            </div>
                        </mat-step>

                        <!-- Schritt 3: Zuordnung -->
                        <mat-step
                            [completed]="requiredSatisfied() && mappingRows().length > 0"
                            [editable]="wizardEditable()"
                        >
                            <ng-template matStepLabel>{{
                                i18n.t('imports.step.mapping')
                            }}</ng-template>
                            <div class="step-body">
                                <p class="hint">
                                    <mat-icon aria-hidden="true">info</mat-icon>
                                    {{ i18n.t('imports.mapping.intro') }}
                                    {{ i18n.t('imports.mapping.constantHint') }}
                                </p>
                                <div class="mapping-grid" data-testid="import-mapping">
                                    @for (
                                        row of mappingRows();
                                        track row.field.name;
                                        let index = $index
                                    ) {
                                        <div
                                            class="mapping-row"
                                            [attr.data-testid]="'import-mapping-' + row.field.name"
                                        >
                                            <div class="mapping-field">
                                                <span class="mapping-label">
                                                    {{ row.field.label }}
                                                    @if (row.field.required) {
                                                        <span
                                                            class="required-marker"
                                                            [attr.aria-label]="
                                                                i18n.t('imports.mapping.required')
                                                            "
                                                            >*</span
                                                        >
                                                    }
                                                </span>
                                                <span class="mapping-kind">{{
                                                    i18n.t(kindKey(row.field.kind))
                                                }}</span>
                                                @if (row.field.description) {
                                                    <span class="mapping-hint">{{
                                                        row.field.description
                                                    }}</span>
                                                }
                                                @if (row.field.allowedValues?.length) {
                                                    <span class="mapping-hint">
                                                        {{
                                                            i18n.t(
                                                                'imports.mapping.allowedValues',
                                                                {
                                                                    values: row.field.allowedValues!.join(
                                                                        ', '
                                                                    ),
                                                                }
                                                            )
                                                        }}
                                                    </span>
                                                }
                                            </div>
                                            <mat-form-field
                                                appearance="outline"
                                                subscriptSizing="dynamic"
                                            >
                                                <mat-label>{{
                                                    i18n.t('imports.mapping.column')
                                                }}</mat-label>
                                                <mat-select
                                                    [value]="row.sourceColumn"
                                                    (selectionChange)="
                                                        setColumn(index, $event.value)
                                                    "
                                                    [attr.data-testid]="
                                                        'import-column-' + row.field.name
                                                    "
                                                >
                                                    <mat-option [value]="null">{{
                                                        i18n.t('imports.mapping.notMapped')
                                                    }}</mat-option>
                                                    @for (column of sourceColumns(); track column) {
                                                        <mat-option [value]="column">{{
                                                            column
                                                        }}</mat-option>
                                                    }
                                                </mat-select>
                                            </mat-form-field>
                                            <mat-form-field
                                                appearance="outline"
                                                subscriptSizing="dynamic"
                                            >
                                                <mat-label>{{
                                                    i18n.t('imports.mapping.constant')
                                                }}</mat-label>
                                                <input
                                                    matInput
                                                    [value]="row.constantValue"
                                                    (input)="
                                                        setConstant(
                                                            index,
                                                            $any($event.target).value
                                                        )
                                                    "
                                                    [attr.data-testid]="
                                                        'import-constant-' + row.field.name
                                                    "
                                                />
                                            </mat-form-field>
                                        </div>
                                    }
                                </div>
                                @if (!requiredSatisfied() && mappingRows().length > 0) {
                                    <p class="hint" data-testid="import-required-missing">
                                        <mat-icon aria-hidden="true">warning</mat-icon>
                                        {{
                                            i18n.t('imports.mapping.requiredMissing', {
                                                fields: missingRequiredLabels(),
                                            })
                                        }}
                                    </p>
                                }
                            </div>
                            <div class="step-actions">
                                <button mat-button type="button" matStepperPrevious>
                                    {{ i18n.t('imports.back') }}
                                </button>
                                <button
                                    mat-flat-button
                                    type="button"
                                    [disabled]="!requiredSatisfied()"
                                    matStepperNext
                                    data-testid="import-to-preview"
                                >
                                    {{ i18n.t('imports.next') }}
                                </button>
                            </div>
                        </mat-step>

                        <!-- Schritt 4: Vorschau -->
                        <mat-step
                            [completed]="!!preview() && preview()!.validRows > 0"
                            [editable]="wizardEditable()"
                        >
                            <ng-template matStepLabel>{{
                                i18n.t('imports.step.preview')
                            }}</ng-template>
                            <div class="step-body">
                                @if (previewLoading()) {
                                    <mat-progress-bar mode="indeterminate" />
                                }
                                @if (previewError(); as message) {
                                    <p class="error-banner" data-testid="import-preview-error">
                                        <mat-icon aria-hidden="true">error_outline</mat-icon
                                        >{{ message }}
                                    </p>
                                }
                                @if (preview(); as result) {
                                    <mat-chip-set data-testid="import-preview-summary">
                                        <mat-chip
                                            class="chip"
                                            data-tone="neutral"
                                            data-testid="import-preview-total"
                                        >
                                            {{
                                                i18n.t('imports.preview.totalRows', {
                                                    count: result.totalRows,
                                                })
                                            }}
                                        </mat-chip>
                                        <mat-chip
                                            class="chip"
                                            data-tone="success"
                                            data-testid="import-preview-valid"
                                        >
                                            {{
                                                i18n.t('imports.preview.validRows', {
                                                    count: result.validRows,
                                                })
                                            }}
                                        </mat-chip>
                                        <mat-chip
                                            class="chip"
                                            data-tone="error"
                                            data-testid="import-preview-invalid"
                                        >
                                            {{
                                                i18n.t('imports.preview.invalidRows', {
                                                    count: result.invalidRows,
                                                })
                                            }}
                                        </mat-chip>
                                    </mat-chip-set>

                                    @if (result.validRows === 0) {
                                        <p class="error-banner" data-testid="import-all-invalid">
                                            <mat-icon aria-hidden="true">block</mat-icon>
                                            {{ i18n.t('imports.preview.allInvalid') }}
                                        </p>
                                    }

                                    <div class="table-card">
                                        <div class="table-scroll">
                                            <table
                                                class="plain-table"
                                                data-testid="import-preview-table"
                                            >
                                                <thead>
                                                    <tr>
                                                        <th>{{ i18n.t('imports.preview.row') }}</th>
                                                        @for (
                                                            field of mappedFields();
                                                            track field.name
                                                        ) {
                                                            <th>{{ field.label }}</th>
                                                        }
                                                        <th>
                                                            {{ i18n.t('imports.preview.errors') }}
                                                        </th>
                                                    </tr>
                                                </thead>
                                                <tbody>
                                                    @for (row of result.rows; track row.rowNumber) {
                                                        <tr
                                                            [class.invalid]="row.errors.length > 0"
                                                            data-testid="import-preview-row"
                                                        >
                                                            <td class="numeric">
                                                                {{ row.rowNumber }}
                                                            </td>
                                                            @for (
                                                                field of mappedFields();
                                                                track field.name
                                                            ) {
                                                                <td>
                                                                    {{
                                                                        row.values[field.name] ??
                                                                            '–'
                                                                    }}
                                                                </td>
                                                            }
                                                            <td class="row-errors">
                                                                @for (
                                                                    error of row.errors;
                                                                    track $index
                                                                ) {
                                                                    <span>{{ error }}</span>
                                                                }
                                                            </td>
                                                        </tr>
                                                    }
                                                </tbody>
                                            </table>
                                        </div>
                                    </div>
                                }
                            </div>
                            <div class="step-actions">
                                <button mat-button type="button" matStepperPrevious>
                                    {{ i18n.t('imports.back') }}
                                </button>
                                <button
                                    mat-flat-button
                                    type="button"
                                    [disabled]="
                                        previewLoading() || !preview() || preview()!.validRows === 0
                                    "
                                    (click)="startImport()"
                                    data-testid="import-execute"
                                >
                                    <mat-icon>play_arrow</mat-icon>
                                    {{ i18n.t('imports.execute') }}
                                </button>
                            </div>
                        </mat-step>

                        <!-- Schritt 5: Ergebnis -->
                        <mat-step [editable]="false">
                            <ng-template matStepLabel>{{
                                i18n.t('imports.step.result')
                            }}</ng-template>
                            <div class="step-body">
                                @if (executing()) {
                                    <div class="executing" data-testid="import-running">
                                        <mat-spinner diameter="40" />
                                        <span>{{ i18n.t('imports.result.running') }}</span>
                                    </div>
                                }
                                @if (executeError(); as message) {
                                    <p class="error-banner" data-testid="import-execute-error">
                                        <mat-icon aria-hidden="true">error_outline</mat-icon
                                        >{{ message }}
                                    </p>
                                }
                                @if (execution(); as result) {
                                    <mat-chip-set data-testid="import-result-summary">
                                        <mat-chip
                                            class="chip"
                                            [attr.data-tone]="statusTone(result.status)"
                                            data-testid="import-result-status"
                                        >
                                            {{ i18n.t(statusKey(result.status)) }}
                                        </mat-chip>
                                        <mat-chip
                                            class="chip"
                                            data-tone="success"
                                            data-testid="import-result-imported"
                                        >
                                            {{ i18n.t('imports.result.imported') }}:
                                            {{ result.importedCount }}
                                        </mat-chip>
                                        <mat-chip
                                            class="chip"
                                            data-tone="error"
                                            data-testid="import-result-failed"
                                        >
                                            {{ i18n.t('imports.result.failed') }}:
                                            {{ result.failedCount }}
                                        </mat-chip>
                                    </mat-chip-set>

                                    @if (result.errors.length > 0) {
                                        <h3 class="section-title">
                                            {{ i18n.t('imports.result.errorList') }}
                                        </h3>
                                        <ul
                                            class="result-errors"
                                            data-testid="import-result-errors"
                                        >
                                            @for (error of result.errors; track $index) {
                                                <li>
                                                    {{
                                                        i18n.t('imports.result.errorRow', {
                                                            row: error.rowNumber,
                                                            message: error.message,
                                                        })
                                                    }}
                                                </li>
                                            }
                                        </ul>
                                    }
                                }
                            </div>
                            <div class="step-actions">
                                <a
                                    mat-stroked-button
                                    routerLink="/import/verlauf"
                                    data-testid="import-result-history"
                                >
                                    <mat-icon>history</mat-icon>
                                    {{ i18n.t('imports.result.toHistory') }}
                                </a>
                                <button
                                    mat-flat-button
                                    type="button"
                                    [disabled]="executing()"
                                    (click)="reset()"
                                    data-testid="import-reset"
                                >
                                    <mat-icon>restart_alt</mat-icon>
                                    {{ i18n.t('imports.result.newImport') }}
                                </button>
                            </div>
                        </mat-step>
                    </mat-stepper>
                </section>
            </div>
        } @else {
            <p>{{ i18n.t('imports.signIn') }}</p>
        }
    `,
    styles: [
        `
            .wizard-card {
                border: 1px solid var(--mat-sys-outline-variant);
                border-radius: var(--mat-sys-corner-large);
                background: var(--mat-sys-surface);
                overflow: hidden;
            }

            .step-body {
                display: flex;
                flex-direction: column;
                gap: 16px;
                padding: 8px 0 16px;
            }

            .step-actions {
                display: flex;
                justify-content: flex-end;
                gap: 8px;
            }

            .file-row {
                display: flex;
                align-items: center;
                gap: 12px;
                flex-wrap: wrap;
            }

            .file-name {
                color: var(--mat-sys-on-surface-variant);
            }

            .database-form {
                display: flex;
                gap: 16px;
                flex-wrap: wrap;
            }

            .database-form mat-form-field {
                flex: 1 1 200px;
            }

            .database-form .grow {
                flex: 2 1 320px;
            }

            .section-title {
                font: var(--mat-sys-title-small);
                margin: 0;
            }

            .plain-table {
                width: 100%;
                border-collapse: collapse;
                font: var(--mat-sys-body-medium);
            }

            .plain-table th,
            .plain-table td {
                padding: 8px 12px;
                text-align: left;
                border-bottom: 1px solid var(--mat-sys-outline-variant);
                white-space: nowrap;
            }

            .plain-table th {
                background: var(--mat-sys-surface-container);
                font: var(--mat-sys-title-small);
            }

            .plain-table tr.invalid td {
                background: var(--mat-sys-error-container);
                color: var(--mat-sys-on-error-container);
            }

            .row-errors {
                color: var(--mat-sys-error);
                display: flex;
                flex-direction: column;
                gap: 2px;
                white-space: normal;
                min-width: 220px;
            }

            .mapping-grid {
                display: flex;
                flex-direction: column;
                gap: 12px;
            }

            .mapping-row {
                display: grid;
                grid-template-columns: minmax(220px, 1.3fr) minmax(200px, 1fr) minmax(200px, 1fr);
                gap: 16px;
                align-items: start;
                padding: 8px 0;
                border-bottom: 1px solid var(--mat-sys-outline-variant);
            }

            .mapping-field {
                display: flex;
                flex-direction: column;
                gap: 2px;
                padding-top: 8px;
            }

            .mapping-label {
                font: var(--mat-sys-title-small);
            }

            .required-marker {
                color: var(--mat-sys-error);
            }

            .mapping-kind {
                color: var(--mat-sys-primary);
                font: var(--mat-sys-label-medium);
            }

            .mapping-hint {
                color: var(--mat-sys-on-surface-variant);
                font: var(--mat-sys-body-small);
            }

            .executing {
                display: flex;
                align-items: center;
                gap: 16px;
                padding: 24px 0;
            }

            .result-errors {
                margin: 0;
                padding-left: 20px;
                color: var(--mat-sys-error);
                display: flex;
                flex-direction: column;
                gap: 4px;
            }

            @media (max-width: 899px) {
                .mapping-row {
                    grid-template-columns: minmax(0, 1fr);
                }
            }
        `,
    ],
})
export class ImportWizardComponent {
    private readonly service = inject(ImportService);
    protected readonly session = inject(SessionService);
    protected readonly i18n = inject(I18nService);

    private readonly stepper = viewChild(MatStepper);

    protected readonly providers = PROVIDERS;
    protected readonly allowedExtensions = IMPORT_FILE_EXTENSIONS.join(', ');
    protected readonly acceptAttribute = IMPORT_FILE_EXTENSIONS.join(',');
    protected readonly maxFileMb = MAX_FILE_MB;

    protected readonly sourceMode = signal<'file' | 'database'>('file');
    protected readonly selectedFile = signal<File | null>(null);
    protected readonly provider = signal<ImportDatabaseProvider>('PostgreSql');
    protected readonly connectionString = signal('');
    protected readonly creating = signal(false);
    protected readonly sourceError = signal<string | null>(null);

    protected readonly importSession = signal<ImportSession | null>(null);
    protected readonly targets = signal<ImportTarget[]>([]);
    protected readonly targetsError = signal<string | null>(null);

    protected readonly selectedTableName = signal<string | null>(null);
    protected readonly selectedEntity = signal<string | null>(null);

    protected readonly mappingRows = signal<MappingRow[]>([]);

    protected readonly preview = signal<ImportPreview | null>(null);
    protected readonly previewLoading = signal(false);
    protected readonly previewError = signal<string | null>(null);

    protected readonly executing = signal(false);
    protected readonly execution = signal<ImportExecution | null>(null);
    protected readonly executeError = signal<string | null>(null);

    protected readonly tables = computed<ImportTable[]>(() => this.importSession()?.tables ?? []);
    protected readonly selectedTable = computed<ImportTable | null>(
        () => this.tables().find((table) => table.name === this.selectedTableName()) ?? null,
    );
    protected readonly selectedTarget = computed<ImportTarget | null>(
        () => this.targets().find((target) => target.entity === this.selectedEntity()) ?? null,
    );
    protected readonly sourceColumns = computed<string[]>(
        () => this.selectedTable()?.columns ?? [],
    );

    protected readonly requiredSatisfied = computed(() =>
        this.mappingRows().every(
            (row) =>
                !row.field.required || row.sourceColumn !== null || row.constantValue.trim() !== '',
        ),
    );
    protected readonly missingRequiredLabels = computed(() =>
        this.mappingRows()
            .filter(
                (row) =>
                    row.field.required &&
                    row.sourceColumn === null &&
                    row.constantValue.trim() === '',
            )
            .map((row) => row.field.label)
            .join(', '),
    );
    protected readonly mappedFields = computed<ImportTargetField[]>(() =>
        this.mappingRows()
            .filter((row) => row.sourceColumn !== null || row.constantValue.trim() !== '')
            .map((row) => row.field),
    );

    /** Going back to fix something is fine until the import has actually run. */
    protected readonly wizardEditable = computed(() => !this.executing() && !this.execution());

    /** Remembers for which table/target pair the auto-mapping ran, so edits survive step changes. */
    private mappedFor: string | null = null;

    constructor() {
        this.session.ready.subscribe((isAuthenticated) => {
            if (isAuthenticated) {
                this.loadTargets();
            }
        });
    }

    protected kindKey(kind: ImportFieldKind): TranslationKey {
        return KIND_KEYS[kind];
    }

    protected statusKey(status: string): TranslationKey {
        switch (status) {
            case 'Completed':
                return 'imports.status.completed';
            case 'CompletedWithErrors':
                return 'imports.status.completedWithErrors';
            case 'Failed':
                return 'imports.status.failed';
            default:
                return 'imports.status.ready';
        }
    }

    protected statusTone(status: string): string {
        switch (status) {
            case 'Completed':
                return 'success';
            case 'CompletedWithErrors':
                return 'warning';
            case 'Failed':
                return 'error';
            default:
                return 'neutral';
        }
    }

    protected onFileSelected(event: Event): void {
        const input = event.target as HTMLInputElement;
        const file = input.files?.[0] ?? null;
        this.sourceError.set(null);
        if (!file) {
            this.selectedFile.set(null);
            return;
        }
        if (file.size > IMPORT_MAX_FILE_BYTES) {
            this.selectedFile.set(null);
            this.sourceError.set(
                this.i18n.t('imports.source.fileTooLarge', { maxMb: MAX_FILE_MB }),
            );
            return;
        }
        const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
        if (!IMPORT_FILE_EXTENSIONS.some((allowed) => allowed === extension)) {
            this.selectedFile.set(null);
            this.sourceError.set(this.i18n.t('imports.source.fileTypeUnsupported'));
            return;
        }
        this.selectedFile.set(file);
    }

    protected uploadFile(): void {
        const file = this.selectedFile();
        if (!file || this.creating()) {
            return;
        }
        this.creating.set(true);
        this.sourceError.set(null);
        this.service.uploadFile(file).subscribe({
            next: (created) => this.onSessionCreated(created),
            error: (error: HttpErrorResponse) => {
                this.creating.set(false);
                this.sourceError.set(this.describe(error, this.i18n.t('imports.error.create')));
            },
        });
    }

    protected connectDatabase(): void {
        const connectionString = this.connectionString().trim();
        if (!connectionString || this.creating()) {
            return;
        }
        this.creating.set(true);
        this.sourceError.set(null);
        this.service.connectDatabase(this.provider(), connectionString).subscribe({
            next: (created) => this.onSessionCreated(created),
            error: (error: HttpErrorResponse) => {
                this.creating.set(false);
                this.sourceError.set(this.describe(error, this.i18n.t('imports.error.create')));
            },
        });
    }

    protected onStepChange(event: StepperSelectionEvent): void {
        if (event.selectedIndex === 2) {
            this.autoMap();
        } else if (event.selectedIndex === 3) {
            this.runPreview();
        }
    }

    protected setColumn(index: number, column: string | null): void {
        this.mappingRows.update((rows) =>
            rows.map((row, i) => (i === index ? { ...row, sourceColumn: column } : row)),
        );
    }

    protected setConstant(index: number, value: string): void {
        this.mappingRows.update((rows) =>
            rows.map((row, i) => (i === index ? { ...row, constantValue: value } : row)),
        );
    }

    protected startImport(): void {
        const session = this.importSession();
        const request = this.buildRequest();
        if (!session || !request || this.executing()) {
            return;
        }
        this.executing.set(true);
        this.executeError.set(null);
        this.execution.set(null);
        this.stepper()?.next();
        this.service.execute(session.id, request).subscribe({
            next: (result) => {
                this.executing.set(false);
                this.execution.set(result);
            },
            error: (error: HttpErrorResponse) => {
                this.executing.set(false);
                this.executeError.set(this.describe(error, this.i18n.t('imports.error.execute')));
            },
        });
    }

    protected reset(): void {
        this.selectedFile.set(null);
        this.connectionString.set('');
        this.creating.set(false);
        this.sourceError.set(null);
        this.importSession.set(null);
        this.selectedTableName.set(null);
        this.selectedEntity.set(null);
        this.mappingRows.set([]);
        this.mappedFor = null;
        this.preview.set(null);
        this.previewError.set(null);
        this.previewLoading.set(false);
        this.execution.set(null);
        this.executeError.set(null);
        this.stepper()?.reset();
    }

    private onSessionCreated(created: ImportSession): void {
        this.creating.set(false);
        this.importSession.set(created);
        this.selectedTableName.set(created.tables?.length === 1 ? created.tables[0].name : null);
        this.selectedEntity.set(this.targets().length === 1 ? this.targets()[0].entity : null);
        this.mappingRows.set([]);
        this.mappedFor = null;
        this.preview.set(null);
        this.previewError.set(null);
        this.stepper()?.next();
    }

    private loadTargets(): void {
        this.service.targets().subscribe({
            next: (targets) => this.targets.set(targets),
            error: (error: HttpErrorResponse) =>
                this.targetsError.set(this.describe(error, this.i18n.t('imports.error.targets'))),
        });
    }

    /**
     * Pre-assigns source columns whose name matches the target field's name or German label
     * (case-insensitive). Runs once per table/target pair so manual corrections survive
     * navigating back and forth.
     */
    private autoMap(): void {
        const table = this.selectedTable();
        const target = this.selectedTarget();
        if (!table || !target) {
            return;
        }
        const key = `${table.name}::${target.entity}`;
        if (this.mappedFor === key) {
            return;
        }
        this.mappedFor = key;
        this.mappingRows.set(
            target.fields.map((field) => ({
                field,
                sourceColumn: this.matchColumn(field, table.columns),
                constantValue: '',
            })),
        );
        this.preview.set(null);
        this.previewError.set(null);
    }

    private matchColumn(field: ImportTargetField, columns: string[]): string | null {
        const name = field.name.toLowerCase();
        const label = field.label.toLowerCase();
        return (
            columns.find((column) => {
                const normalized = column.trim().toLowerCase();
                return normalized === name || normalized === label;
            }) ?? null
        );
    }

    private buildRequest(): ImportMappingRequest | null {
        const table = this.selectedTable();
        const target = this.selectedTarget();
        if (!table || !target) {
            return null;
        }
        return {
            tableName: table.name,
            targetEntity: target.entity,
            mappings: this.mappingRows()
                .filter((row) => row.sourceColumn !== null || row.constantValue.trim() !== '')
                .map((row) => ({
                    targetField: row.field.name,
                    sourceColumn: row.sourceColumn,
                    constantValue:
                        row.constantValue.trim() === '' ? null : row.constantValue.trim(),
                })),
        };
    }

    private runPreview(): void {
        const session = this.importSession();
        const request = this.buildRequest();
        if (!session || !request) {
            return;
        }
        this.previewLoading.set(true);
        this.previewError.set(null);
        this.preview.set(null);
        this.service.preview(session.id, request, PREVIEW_ROWS).subscribe({
            next: (result) => {
                this.previewLoading.set(false);
                this.preview.set(result);
            },
            error: (error: HttpErrorResponse) => {
                this.previewLoading.set(false);
                this.previewError.set(this.describe(error, this.i18n.t('imports.error.preview')));
            },
        });
    }

    /** Problem-detail aware error text: validation errors > detail > title > fallback. */
    private describe(error: HttpErrorResponse, fallback: string): string {
        if (error.status === 503) {
            return `${this.i18n.t('imports.error.unreachable')} (HTTP 503)`;
        }
        if (error.status === 413) {
            return `${this.i18n.t('imports.source.fileTooLarge', { maxMb: MAX_FILE_MB })} (HTTP 413)`;
        }
        const problem = error.error as {
            title?: string;
            detail?: string;
            errors?: Record<string, string[]>;
        } | null;
        const validationMessages = problem?.errors
            ? Object.values(problem.errors)
                  .flat()
                  .filter((message) => !!message)
                  .join(' ')
            : '';
        const sentence =
            validationMessages !== ''
                ? validationMessages
                : (problem?.detail ?? problem?.title ?? fallback);
        return `${sentence} (HTTP ${error.status})`;
    }
}
