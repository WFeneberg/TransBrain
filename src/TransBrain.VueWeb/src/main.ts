import { createApp } from 'vue';
import { createPinia } from 'pinia';
import { createRouter, createWebHistory } from 'vue-router';
import { createVuetify } from 'vuetify';
import * as components from 'vuetify/components';
import * as directives from 'vuetify/directives';
import 'vuetify/styles';
import App from './App.vue';
import VehicleList from './views/VehicleList.vue';
import AuthCallback from './views/AuthCallback.vue';

const router = createRouter({
    history: createWebHistory(),
    routes: [
        { path: '/', component: VehicleList },
        { path: '/callback', component: AuthCallback },
    ],
});

createApp(App)
    .use(createPinia())
    .use(router)
    .use(createVuetify({ components, directives }))
    .mount('#app');
