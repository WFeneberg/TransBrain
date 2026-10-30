<script setup lang="ts">
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';

// Markers are drawn as numbered/lettered divIcons and the route as a plain polyline: both avoid
// Leaflet's default marker PNGs, which Vite does not resolve.
interface LocationMarker {
    latitude: number;
    longitude: number;
    text: string;
    title: string;
}

interface LocationPoint {
    latitude: number;
    longitude: number;
}

const props = defineProps<{
    markers: LocationMarker[];
    polyline?: LocationPoint[];
}>();

const container = ref<HTMLDivElement | null>(null);
let map: L.Map | null = null;
let layer: L.LayerGroup | null = null;

// Germany as the fallback view when there is nothing to fit.
const fallbackCenter: L.LatLngTuple = [51.1, 10.4];

onMounted(() => {
    map = L.map(container.value!).setView(fallbackCenter, 5);
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        attribution: '© OpenStreetMap contributors',
        maxZoom: 19,
    }).addTo(map);
    layer = L.layerGroup().addTo(map);
    draw();
    // A map created while its container is still laying out measures 0x0 and renders grey.
    nextTick(() => map?.invalidateSize());
});

watch(() => [props.markers, props.polyline], draw, { deep: true });

onBeforeUnmount(() => {
    map?.remove();
    map = null;
    layer = null;
});

function draw(): void {
    if (!map || !layer) {
        return;
    }

    layer.clearLayers();
    const points: L.LatLngTuple[] = [];

    for (const marker of props.markers) {
        const position: L.LatLngTuple = [marker.latitude, marker.longitude];
        points.push(position);
        const icon = L.divIcon({ className: 'location-map-pin', html: marker.text, iconSize: [28, 28], iconAnchor: [14, 14] });
        // The title is user data (names, addresses): set as text, never as HTML.
        const title = document.createElement('span');
        title.textContent = marker.title;
        L.marker(position, { icon }).bindTooltip(title).addTo(layer);
    }

    if (props.polyline?.length) {
        const line = props.polyline.map((point): L.LatLngTuple => [point.latitude, point.longitude]);
        L.polyline(line, { color: '#1565c0', weight: 4 }).addTo(layer);
        points.push(...line);
    }

    if (points.length > 0) {
        map.fitBounds(L.latLngBounds(points), { padding: [24, 24], maxZoom: 14 });
    } else {
        map.setView(fallbackCenter, 5);
    }
}
</script>

<template>
    <div ref="container" class="location-map"></div>
</template>

<style>
.location-map {
    width: 100%;
    height: 400px;
}

.location-map-pin {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 28px;
    height: 28px;
    border: 2px solid #fff;
    border-radius: 50%;
    background: #1565c0;
    color: #fff;
    font-weight: 700;
    box-shadow: 0 1px 4px rgba(0, 0, 0, 0.4);
}
</style>
