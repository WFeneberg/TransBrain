import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/** Mirror of AssistantSettingsResponse in the API. The stored API key itself never arrives. */
export interface AssistantSettings {
    provisioned: boolean;
    enabled: boolean;
    provider: string;
    model: string;
    customBaseUrl: string | null;
    autonomousActionsEnabled: boolean;
    hasApiKey: boolean;
    knownProviders: string[];
}

/** Mirror of AssistantSettingsPayload in the API; apiKey null keeps the stored key. */
export interface AssistantSettingsPayload {
    enabled: boolean;
    provider: string;
    model: string;
    customBaseUrl: string | null;
    autonomousActionsEnabled: boolean;
    apiKey: string | null;
}

/**
 * The system administrator's view of the shared agent container. These settings configure the
 * installation, not a tenant, so they live under `/api/system` and need no tenant selection.
 */
@Injectable({ providedIn: 'root' })
export class AssistantSettingsService {
    private readonly http = inject(HttpClient);

    getSettings(): Observable<AssistantSettings> {
        return this.http.get<AssistantSettings>('/api/system/assistant-settings');
    }

    updateSettings(payload: AssistantSettingsPayload): Observable<AssistantSettings> {
        return this.http.put<AssistantSettings>('/api/system/assistant-settings', payload);
    }
}
