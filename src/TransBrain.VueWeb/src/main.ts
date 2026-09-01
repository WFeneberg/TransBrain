import { createApp } from 'vue';
import { createPinia } from 'pinia';
import { createRouter, createWebHistory } from 'vue-router';
import { createVuetify } from 'vuetify';
import * as components from 'vuetify/components';
import * as directives from 'vuetify/directives';
import 'vuetify/styles';
import App from './App.vue';
import Home from './views/Home.vue';
import VehicleList from './views/VehicleList.vue';
import VehicleForm from './views/VehicleForm.vue';
import DriverList from './views/DriverList.vue';
import DriverForm from './views/DriverForm.vue';
import OrderList from './views/OrderList.vue';
import OrderForm from './views/OrderForm.vue';
import TourList from './views/TourList.vue';
import TourForm from './views/TourForm.vue';
import TourDetail from './views/TourDetail.vue';
import AuthCallback from './views/AuthCallback.vue';
import { useAuthStore } from './stores/auth';
import type { Capability } from './auth/capabilities';

const router = createRouter({
    history: createWebHistory(),
    routes: [
        { path: '/', component: Home },
        { path: '/vehicles', component: VehicleList },
        // 'new' must be registered before ':id' - the router matches path segments in order,
        // and a ':id' route registered first would swallow '/vehicles/new' by treating "new"
        // as an id.
        { path: '/vehicles/new', component: VehicleForm, meta: { capability: 'masterData.write' } },
        { path: '/vehicles/:id', component: VehicleForm, meta: { capability: 'masterData.write' } },
        { path: '/drivers', component: DriverList },
        { path: '/drivers/new', component: DriverForm, meta: { capability: 'masterData.write' } },
        { path: '/drivers/:id', component: DriverForm, meta: { capability: 'masterData.write' } },
        { path: '/orders', component: OrderList },
        { path: '/orders/new', component: OrderForm, meta: { capability: 'dispatch.write' } },
        { path: '/orders/:id', component: OrderForm, meta: { capability: 'dispatch.write' } },
        { path: '/tours', component: TourList },
        { path: '/tours/new', component: TourForm, meta: { capability: 'dispatch.write' } },
        // No capability: the fahrer must reach this to start their tour, and a viewer may look.
        // The start/complete/assign buttons inside are gated individually.
        { path: '/tours/:id', component: TourDetail },
        { path: '/callback', component: AuthCallback },
    ],
});

/**
 * Runs before every navigation, including the very first. It awaits auth.load() rather than
 * reading the store as it happens to stand: on a directly opened URL - a reload, a bookmark -
 * nothing has hydrated the stored session yet, and the guard would judge a signed-in user as
 * signed out.
 *
 * '/callback' is skipped deliberately: the OIDC code is only exchanged inside AuthCallback.vue,
 * so at that moment there is legitimately no session yet, and bouncing it to '/' would break
 * every sign-in.
 *
 * The list routes carry no capability. The API lets every role read them (Policies.Read), so a
 * client-side block would be stricter than the server for no reason - hiding a tile means "you
 * do not need this", not "you may not have this".
 */
router.beforeEach(async (to) => {
    if (to.path === '/callback') {
        return true;
    }

    const auth = useAuthStore();
    await auth.load();

    if (!auth.isAuthenticated) {
        return to.path === '/' ? true : '/';
    }

    const required = to.meta.capability as Capability | undefined;
    return required && !auth.can(required) ? '/' : true;
});

createApp(App)
    .use(createPinia())
    .use(router)
    .use(createVuetify({ components, directives }))
    .mount('#app');
