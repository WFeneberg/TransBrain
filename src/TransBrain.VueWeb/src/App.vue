<script setup lang="ts">
import { onMounted } from 'vue';
import { useAuthStore } from './stores/auth';

const auth = useAuthStore();

// The one place the stored session is hydrated. Every view used to call auth.load() itself; the
// router guard in main.ts awaits the same call, so a directly opened URL is covered too.
onMounted(async () => {
    await auth.load();
});
</script>

<template>
    <v-app>
        <v-app-bar>
            <v-app-bar-title>TransBrain</v-app-bar-title>
            <template v-if="auth.isAuthenticated">
                <v-btn to="/" data-testid="nav-home">Home</v-btn>
                <v-btn v-if="auth.areas.has('vehicles')" to="/vehicles" data-testid="nav-vehicles">Vehicles</v-btn>
                <v-btn v-if="auth.areas.has('drivers')" to="/drivers" data-testid="nav-drivers">Drivers</v-btn>
                <v-btn v-if="auth.areas.has('orders')" to="/orders" data-testid="nav-orders">Orders</v-btn>
                <v-btn v-if="auth.areas.has('tours')" to="/tours" data-testid="nav-tours">Tours</v-btn>
                <v-spacer />
                <span data-testid="nav-user">{{ auth.displayName }}</span>
                <v-btn data-testid="logout" @click="auth.logout()">Sign out</v-btn>
            </template>
        </v-app-bar>
        <v-main>
            <router-view />
        </v-main>
    </v-app>
</template>
