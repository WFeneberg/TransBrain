<script setup lang="ts">
import { ref, onMounted } from 'vue';
import { useRouter } from 'vue-router';
import { useAuthStore } from '../stores/auth';

// This dedicated route exists because oidc-client-ts requires signinRedirectCallback() to run
// on a route matching the configured redirect_uri (see auth/userManager.ts) - it will not
// process the authorization code from wherever the app happens to mount. The Angular app has
// no equivalent route: angular-auth-oidc-client's checkAuth() processes the callback wherever
// it runs, so its list component is mounted at the root instead. Do NOT collapse this route to
// match the Angular shape - doing so on the Angular side once already silently discarded the
// authorization code and cost a debugging round to track down. The two frontends differ here
// because the two OIDC libraries genuinely differ, not by oversight.
const auth = useAuthStore();
const router = useRouter();
const errorMessage = ref<string | null>(null);

onMounted(async () => {
    try {
        await auth.completeLogin();
        await router.replace('/');
    } catch {
        // A rejected code, a state mismatch, or an expired signin attempt throws here. Without
        // this catch, router.replace('/') never runs and the user is left on the spinner below
        // forever, with no explanation and no way back - the same failure the vehicle list guards
        // against, just one screen earlier. No retry loop, no toast: just a message and a way to
        // restart the flow. The raw error is deliberately not logged or displayed.
        errorMessage.value = 'Sign-in could not be completed. Please try again.';
    }
});
</script>

<template>
    <v-container v-if="errorMessage">
        <p data-testid="callback-error">{{ errorMessage }}</p>
        <v-btn data-testid="login" @click="auth.login()">Sign in</v-btn>
    </v-container>
    <v-progress-circular v-else indeterminate />
</template>
