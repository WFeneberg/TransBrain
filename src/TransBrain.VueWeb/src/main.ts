import { createApp } from 'vue';
import { createPinia } from 'pinia';
import { createRouter, createWebHistory } from 'vue-router';
import { createVuetify } from 'vuetify';
import * as components from 'vuetify/components';
import * as directives from 'vuetify/directives';
import 'vuetify/styles';
import App from './App.vue';
import VehicleList from './views/VehicleList.vue';
import VehicleForm from './views/VehicleForm.vue';
import DriverList from './views/DriverList.vue';
import DriverForm from './views/DriverForm.vue';
import OrderList from './views/OrderList.vue';
import OrderForm from './views/OrderForm.vue';
import AuthCallback from './views/AuthCallback.vue';

const router = createRouter({
    history: createWebHistory(),
    routes: [
        { path: '/', component: VehicleList },
        // Two canonical URLs for one screen, mirroring the Angular app's app.routes.ts: '/'
        // must stay the primary route (see AuthCallback.vue on why '/' is load-bearing for the
        // Vue OIDC callback too), and '/vehicles' is kept reachable as a second route pointing
        // at the same component so the vehicle form's save/cancel has somewhere named to go.
        { path: '/vehicles', component: VehicleList },
        // 'new' must be registered before ':id' - the router matches path segments in order,
        // and a ':id' route registered first would swallow '/vehicles/new' by treating "new"
        // as an id.
        { path: '/vehicles/new', component: VehicleForm },
        { path: '/vehicles/:id', component: VehicleForm },
        { path: '/drivers', component: DriverList },
        { path: '/drivers/new', component: DriverForm },
        { path: '/drivers/:id', component: DriverForm },
        { path: '/orders', component: OrderList },
        { path: '/orders/new', component: OrderForm },
        { path: '/orders/:id', component: OrderForm },
        { path: '/callback', component: AuthCallback },
    ],
});

createApp(App)
    .use(createPinia())
    .use(router)
    .use(createVuetify({ components, directives }))
    .mount('#app');
