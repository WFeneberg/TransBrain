import { defineStore } from 'pinia';
import { ref } from 'vue';
import type { User } from 'oidc-client-ts';
import { userManager } from '../auth/userManager';

export const useAuthStore = defineStore('auth', () => {
    const user = ref<User | null>(null);
    const isAuthenticated = ref(false);

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

    return { user, isAuthenticated, load, login, completeLogin };
});
