import {
  afterNextRender,
  Component,
  DestroyRef,
  effect,
  ElementRef,
  inject,
  input,
  output,
  signal,
  viewChild,
  ViewEncapsulation,
} from '@angular/core';
import {
  control,
  divIcon,
  latLngBounds,
  layerGroup,
  map,
  marker,
  polyline,
  tileLayer,
} from 'leaflet';
import type { LatLngTuple, Map as LeafletMap, Marker } from 'leaflet';
import type { FixedStop } from '../api/itineraries-api';

const italy: LatLngTuple = [42.6, 12.45];

function position(stop: FixedStop): LatLngTuple | null {
  const { latitude, longitude } = stop;
  return Number.isFinite(latitude) &&
    Number.isFinite(longitude) &&
    Math.abs(latitude) <= 90 &&
    Math.abs(longitude) <= 180
    ? [latitude, longitude]
    : null;
}

@Component({
  selector: 'app-route-map',
  templateUrl: './route-map.html',
  styleUrl: './route-map.css',
  encapsulation: ViewEncapsulation.None,
})
export class RouteMap {
  readonly stops = input.required<readonly FixedStop[]>();
  readonly activeStopId = input<string | null>(null);
  readonly area = input('Italia');
  readonly stopSelected = output<string>();

  private readonly surface = viewChild<ElementRef<HTMLElement>>('surface');
  private readonly destroyRef = inject(DestroyRef);
  private readonly instance = signal<LeafletMap | null>(null);
  private readonly route = layerGroup();
  private readonly markers = new Map<string, Marker>();
  protected readonly tilesUnavailable = signal(false);

  constructor() {
    afterNextRender(() => {
      const element = this.surface()?.nativeElement;
      if (!element) return;

      const instance = map(element, {
        zoomControl: false,
        scrollWheelZoom: false,
        minZoom: 3,
      }).setView(italy, 6);
      instance.attributionControl.setPrefix(false);
      control
        .zoom({ position: 'bottomright', zoomInTitle: 'Ingrandisci', zoomOutTitle: 'Riduci' })
        .addTo(instance);

      const tiles = tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        className: 'route-map__tiles',
        attribution: '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
      });
      let tileLoaded = false;
      tiles.on('tileload', () => {
        tileLoaded = true;
        this.tilesUnavailable.set(false);
      });
      tiles.on('load', () => this.tilesUnavailable.set(!tileLoaded));
      tiles.addTo(instance);

      this.route.addTo(instance);
      this.instance.set(instance);
      const resize = new ResizeObserver(() => {
        instance.invalidateSize({ pan: false });
        this.center();
      });
      resize.observe(element);
      this.destroyRef.onDestroy(() => {
        resize.disconnect();
        instance.remove();
      });
    });

    effect(() => {
      const instance = this.instance();
      const stops = this.stops();
      if (!instance) return;
      this.route.clearLayers();
      this.markers.clear();

      const points = stops.map(position);
      const legs: LatLngTuple[][] = [];
      points.forEach((point, index) => {
        const next = points[index + 1];
        if (point && next) legs.push([point, next]);
      });
      if (legs.length) {
        polyline(legs, { color: '#ffffff', weight: 8, opacity: 0.92, interactive: false }).addTo(
          this.route,
        );
        polyline(legs, { color: '#19654d', weight: 4, opacity: 1, interactive: false }).addTo(
          this.route,
        );
      }

      points.forEach((point, index) => {
        if (!point) return;
        const stop = stops[index];
        const label = document.createElement('span');
        label.textContent = stop.name;
        const pin = marker(point, {
          icon: divIcon({
            className: 'route-map__marker',
            html: String(index + 1),
            iconSize: [29, 29],
            iconAnchor: [14, 14],
          }),
          title: stop.name,
        }).bindTooltip(label, { direction: 'top', offset: [0, -15] });
        pin.on('click', () => this.stopSelected.emit(stop.id)).addTo(this.route);
        pin.getElement()?.setAttribute('aria-label', `Tappa ${index + 1}. ${stop.name}`);
        this.markers.set(stop.id, pin);
      });
      this.center();
    });

    effect(() => {
      const instance = this.instance();
      const activeId = this.activeStopId();
      this.stops();
      if (!instance) return;
      this.markers.forEach((pin, id) => {
        pin.getElement()?.classList.toggle('route-map__marker--active', id === activeId);
        if (id === activeId) {
          instance.panTo(pin.getLatLng(), { animate: false });
          pin.openTooltip();
        } else {
          pin.closeTooltip();
        }
      });
    });
  }

  protected center(): void {
    const instance = this.instance();
    if (!instance) return;
    const points = Array.from(this.markers.values(), (pin) => pin.getLatLng());
    if (points.length) {
      instance.fitBounds(latLngBounds(points), { padding: [54, 54], maxZoom: 13, animate: false });
    } else {
      instance.setView(italy, 6, { animate: false });
    }
  }
}
