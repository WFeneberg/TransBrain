import axios from 'axios';
import { userManager } from '../auth/userManager';

/** Mirror of AssistantStatusResponse in the API: whether the chat should be offered at all. */
export interface AssistantStatus {
    enabled: boolean;
    ready: boolean;
}

/** One turn of the conversation. 'system' is deliberately absent: the server builds its own. */
export interface AssistantChatMessage {
    role: 'user' | 'assistant';
    content: string;
}

/** Mirror of AssistantSettingsResponse in the API. The key itself never travels here. */
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

/** Mirror of AssistantSettingsPayload in the API. apiKey null keeps the stored key. */
export interface AssistantSettingsUpdate {
    enabled: boolean;
    provider: string;
    model: string;
    customBaseUrl: string | null;
    autonomousActionsEnabled: boolean;
    apiKey: string | null;
}

/**
 * The agent may use tools (database lookups, API calls) before it answers, so a reply can
 * legitimately take minutes. Axios would otherwise never time out (default 0), but an explicit
 * bound keeps a hung gateway from blocking the chat forever.
 */
const CHAT_TIMEOUT_MS = 300_000;

const client = axios.create({ baseURL: '/api' });

client.interceptors.request.use(async (config) => {
    const user = await userManager.getUser();
    if (user?.access_token) {
        config.headers.Authorization = `Bearer ${user.access_token}`;
    }
    return config;
});

/** Whether the signed-in user should see the chat launcher at all. */
export async function getAssistantStatus(): Promise<AssistantStatus> {
    const response = await client.get<AssistantStatus>('/assistant/status');
    return response.data;
}

/**
 * Sends the FULL conversation; the backend is stateless and prepends its own system message.
 * The caller trims to the server's 50-message cap before handing the list over.
 */
export async function sendAssistantChat(
    messages: AssistantChatMessage[],
    pageContext: string | null,
): Promise<string> {
    const response = await client.post<{ reply: string }>(
        '/assistant/chat',
        { messages, pageContext },
        { timeout: CHAT_TIMEOUT_MS },
    );
    return response.data.reply;
}

/** Current central configuration; system-admin only. */
export async function getAssistantSettings(): Promise<AssistantSettings> {
    const response = await client.get<AssistantSettings>('/system/assistant-settings');
    return response.data;
}

/** Writes the central configuration; system-admin only. Returns the stored state. */
export async function updateAssistantSettings(update: AssistantSettingsUpdate): Promise<AssistantSettings> {
    const response = await client.put<AssistantSettings>('/system/assistant-settings', update);
    return response.data;
}
