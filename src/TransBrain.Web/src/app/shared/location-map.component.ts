import {
    AfterViewInit,
    Component,
    ElementRef,
    OnDestroy,
    computed,
    effect,
    input,
    viewChild,
} from '@angular/core';
import * as L from 'leaflet';

export interface MapPoint {
    latitude: number;
    longitude: number;
}

/** A numbered or lettered pin. `label` is shown on the pin, `title` in its tooltip. */
export interface MapMarker extends MapPoint {
    label: string;
    title: string;
}

const TILE_URL = 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';
const ATTRIBUTION = '© OpenStreetMap contributors';

/**
 * Reusable Leaflet map. Pins are drawn as HTML badges rather than Leaflet's default marker
 * images, because those PNG paths break under the Angular bundler.
 */
@Component({
    selector: 'app-location-map',
    standalone: true,
    template: `<div #container class="location-map" data-testid="location-map"></div>`,
    styles: [
        `
            .location-map {
                height: 360px;
                width: 100%;
                max-width: 100%;
            }
        `,
    ],
})
export class LocationMapComponent implements AfterViewInit, OnDestroy {
    readonly markers = input<MapMarker[]>([]);
    readonly route = input<MapPoint[]>([]);

    private readonly container = viewChild.required<ElementRef<HTMLElement>>('container');
    private map: L.Map | null = null;
    private layer: L.LayerGroup | null = null;

    private readonly points = computed<MapPoint[]>(() => [...this.markers(), ...this.route()]);

    constructor() {
        effect(() => {
            this.points();
            this.draw();
        });
    }

    ngAfterViewInit(): void {
        this.map = L.map(this.container().nativeElement, { attributionControl: true }).setView([51.1, 10.4], 5);
        L.tileLayer(TILE_URL, { attribution: ATTRIBUTION, maxZoom: 19 }).addTo(this.map);
        this.layer = L.layerGroup().addTo(this.map);
        this.draw();
        // The container may have been laid out at zero size before it became visible; Leaflet
        // then renders grey tiles until it re-measures.
        setTimeout(() => this.map?.invalidateSize());
    }

    ngOnDestroy(): void {
        this.map?.remove();
        this.map = null;
        this.layer = null;
    }

    private draw(): void {
        if (!this.map || !this.layer) {
            return;
        }
        this.layer.clearLayers();

        const route = this.route();
        if (route.length > 1) {
            L.polyline(
                route.map((p) => L.latLng(p.latitude, p.longitude)),
                { color: '#1565c0', weight: 4 },
            ).addTo(this.layer);
        }

        for (const marker of this.markers()) {
            L.marker([marker.latitude, marker.longitude], { icon: this.badge(marker.label) })
                .bindTooltip(marker.title)
                .addTo(this.layer);
        }

        const points = this.points();
        if (points.length === 1) {
            this.map.setView([points[0].latitude, points[0].longitude], 13);
        } else if (points.length > 1) {
            const bounds = L.latLngBounds(points.map((p) => [p.latitude, p.longitude] as L.LatLngTuple));
            this.map.fitBounds(bounds, { padding: [30, 30] });
        }
    }

    private badge(label: string): L.DivIcon {
        return L.divIcon({
            className: '',
            iconSize: [26, 26],
            iconAnchor: [13, 13],
            html:
                `<span style="display:flex;align-items:center;justify-content:center;width:26px;height:26px;` +
                `border-radius:50%;background:#1565c0;color:#fff;border:2px solid #fff;` +
                `box-shadow:0 1px 3px rgba(0,0,0,.4);font:600 12px Roboto,sans-serif;">${label}</span>`,
        });
    }
}
