import { defineStore } from 'pinia';
import { computed, ref } from 'vue';
import type { User } from 'oidc-client-ts';
import { userManager } from '../auth/userManager';
import { areasFor, capabilitiesFor, knownRoles, type AppRole, type Area, type Capability } from '../auth/capabilities';

export const useAuthStore = defineStore('auth', () => {
    const user = ref<User | null>(null);
    const isAuthenticated = ref(false);

    /**
     * The roles come from the id token's realm_access claim, which the realm's
     * realm-roles-in-id-token mapper puts there. Without that mapper Keycloak writes realm_access
     * to the access token only, and profile carries no roles at all.
     */
    const roles = computed<AppRole[]>(() => {
        const raw = (user.value?.profile as { realm_access?: { roles?: unknown } } | undefined)?.realm_access?.roles;
        const values = Array.isArray(raw) ? raw.filter((role): role is string => typeof role === 'string') : [];
        return knownRoles(values);
    });

    const displayName = computed<string>(() => {
        const profile = user.value?.profile;
        if (typeof profile?.name === 'string' && profile.name.length > 0) {
            return profile.name;
        }
        return typeof profile?.preferred_username === 'string' ? profile.preferred_username : '';
    });

    const areas = computed<ReadonlySet<Area>>(() => areasFor(roles.value));
    const capabilities = computed<ReadonlySet<Capability>>(() => capabilitiesFor(roles.value));

    async function load(): Promise<void> {
        user.value = await userManager.getUser();
        isAuthenticated.value = user.value !== null && !user.value.expired;
    }

    async function login(): Promise<void> {
        await userManager.signinRedirect();
    }

    async function completeLogin(): Promise<void> {
        user.value = await userManager.signinRedirectCallback();
        isAuthenticated.value = true;
    }

    /**
     * RP-initiated logout. Without it there is no way to change role in a running browser other
     * than a private window, which makes a role-aware UI untestable by hand. The realm allows the
     * post-logout redirect for both frontend ports (transbrain-realm.json).
     */
    async function logout(): Promise<void> {
        await userManager.signoutRedirect();
    }

    function can(capability: Capability): boolean {
        return capabilities.value.has(capability);
    }

    function hasRole(role: AppRole): boolean {
        return roles.value.includes(role);
    }

    return {
        user,
        isAuthenticated,
        roles,
        displayName,
        areas,
        load,
        login,
        completeLogin,
        logout,
        can,
        hasRole,
    };
});
