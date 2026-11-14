<script setup lang="ts">
import { onMounted } from 'vue';
import { useLocale } from 'vuetify';
import { useAuthStore } from './stores/auth';
import { useI18n, type Locale } from './i18n';

const auth = useAuthStore();
const { locale, setLocale, t } = useI18n();
const vuetifyLocale = useLocale();

// The one place the stored session is hydrated. Every view used to call auth.load() itself; the
// router guard in main.ts awaits the same call, so a directly opened URL is covered too.
onMounted(async () => {
    await auth.load();
});

function toggleLocale(): void {
    const next: Locale = locale.value === 'de' ? 'en' : 'de';
    setLocale(next);
    vuetifyLocale.current.value = next;
}
</script>

<template>
    <v-app>
        <v-app-bar>
            <v-app-bar-title>TransBrain</v-app-bar-title>
            <template v-if="auth.isAuthenticated">
                <v-btn to="/" data-testid="nav-home">{{ t('nav.home') }}</v-btn>
                <v-btn v-if="auth.areas.has('vehicles')" to="/vehicles" data-testid="nav-vehicles">{{ t('nav.vehicles') }}</v-btn>
                <v-btn v-if="auth.areas.has('drivers')" to="/drivers" data-testid="nav-drivers">{{ t('nav.drivers') }}</v-btn>
                <v-btn v-if="auth.areas.has('customers')" to="/customers" data-testid="nav-customers">{{ t('nav.customers') }}</v-btn>
                <v-btn v-if="auth.areas.has('orders')" to="/orders" data-testid="nav-orders">{{ t('nav.orders') }}</v-btn>
                <v-btn v-if="auth.areas.has('tours')" to="/tours" data-testid="nav-tours">{{ t('nav.tours') }}</v-btn>
                <v-btn v-if="auth.areas.has('invoices')" to="/invoices" data-testid="nav-invoices">{{ t('nav.invoices') }}</v-btn>
                <v-btn v-if="auth.can('masterData.write')" to="/geodata" data-testid="nav-geodata">{{ t('nav.geodata') }}</v-btn>
                <v-btn v-if="auth.can('maintenance') || auth.can('masterData.write')" to="/maintenance" data-testid="nav-maintenance">{{ t('nav.maintenance') }}</v-btn>
            </template>
            <v-spacer />
            <!-- Hidden on phone widths: the driver's home page is meant to be usable in
                 the cab, and there the name pushes the nav links and Sign out into each
                 other. The name is the least load-bearing thing in the bar. -->
            <span v-if="auth.isAuthenticated" class="d-none d-sm-inline" data-testid="nav-user">{{ auth.displayName }}</span>
            <v-btn data-testid="lang-switch" @click="toggleLocale">{{ locale === 'de' ? 'EN' : 'DE' }}</v-btn>
            <v-btn v-if="auth.isAuthenticated" data-testid="logout" @click="auth.logout()">{{ t('common.signOut') }}</v-btn>
        </v-app-bar>
        <v-main>
            <router-view />
        </v-main>
    </v-app>
</template>
