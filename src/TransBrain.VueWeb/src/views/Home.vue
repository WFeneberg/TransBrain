<script setup lang="ts">
import { useAuthStore } from '../stores/auth';
import type { Area, Capability } from '../auth/capabilities';

interface AreaTile {
    area: Area;
    title: string;
    description: string;
    route: string;
    addLabel: string;
    addRoute: string;
    addCapability: Capability;
}

const tiles: readonly AreaTile[] = [
    {
        area: 'vehicles',
        title: 'Vehicles',
        description: 'Fleet master data: plates, payload, inspections.',
        route: '/vehicles',
        addLabel: 'Add vehicle',
        addRoute: '/vehicles/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'drivers',
        title: 'Drivers',
        description: 'Driver master data: licences, availability.',
        route: '/drivers',
        addLabel: 'Add driver',
        addRoute: '/drivers/new',
        addCapability: 'masterData.write',
    },
    {
        area: 'orders',
        title: 'Orders',
        description: 'Transport orders from pickup to delivery.',
        route: '/orders',
        addLabel: 'New order',
        addRoute: '/orders/new',
        addCapability: 'dispatch.write',
    },
    {
        area: 'tours',
        title: 'Tours',
        description: 'Plan orders onto vehicles and follow execution.',
        route: '/tours',
        addLabel: 'Plan tour',
        addRoute: '/tours/new',
        addCapability: 'dispatch.write',
    },
];

const auth = useAuthStore();
</script>

<template>
    <v-container>
        <template v-if="auth.isAuthenticated">
            <h1 data-testid="home-greeting">Welcome, {{ auth.displayName }}</h1>
            <v-chip v-for="role in auth.roles" :key="role" data-testid="home-role-chip">{{ role }}</v-chip>

            <v-row class="mt-4">
                <template v-for="tile in tiles" :key="tile.area">
                    <v-col v-if="auth.areas.has(tile.area)" cols="12" md="3">
                        <v-card :data-testid="`home-tile-${tile.area}`">
                            <v-card-title>{{ tile.title }}</v-card-title>
                            <v-card-text>{{ tile.description }}</v-card-text>
                            <v-card-actions>
                                <v-btn :to="tile.route">Open</v-btn>
                                <v-btn
                                    v-if="auth.can(tile.addCapability)"
                                    :to="tile.addRoute"
                                    :data-testid="`home-tile-${tile.area}-add`"
                                    >{{ tile.addLabel }}</v-btn
                                >
                            </v-card-actions>
                        </v-card>
                    </v-col>
                </template>
            </v-row>
        </template>
        <v-btn v-else data-testid="login" @click="auth.login()">Sign in</v-btn>
    </v-container>
</template>
