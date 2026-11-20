import { Component, computed, input, model, signal } from '@angular/core';
import { CatalogStop, DraftStopChange, FixedStop, StopDetails } from '../api/itineraries-api';
import { StopForm } from './stop-form';

export interface EditorStop extends FixedStop {
  change?: DraftStopChange;
}

@Component({
  selector: 'app-stop-picker',
  imports: [StopForm],
  templateUrl: './stop-picker.html',
})
export class StopPicker {
  readonly catalog = input.required<CatalogStop[]>();
  readonly selected = model.required<EditorStop[]>();
  readonly error = input<string | null>(null);
  readonly loading = input(false);
  readonly editing = model(false);

  private nextTemporaryId = 0;
  private readonly editingIndex = signal<number | null>(null);
  protected readonly editingStop = computed(() => {
    const index = this.editingIndex();
    return index === null ? null : this.selected()[index];
  });

  protected readonly query = signal('');
  protected readonly choice = signal('');
  protected readonly available = computed(() => {
    const selectedIds = new Set(this.selected().map((stop) => stop.id));
    const query = this.searchText(this.query().trim());
    return this.catalog().filter(
      (stop) =>
        !selectedIds.has(stop.id) &&
        this.searchText(`${stop.name} ${stop.municipality ?? ''} ${stop.province ?? ''}`).includes(
          query,
        ),
    );
  });
  protected readonly candidate = computed(() =>
    this.available().find(({ id }) => id === this.choice()),
  );

  protected search(value: string): void {
    this.query.set(value);
    this.choice.set('');
  }

  protected add(): void {
    const stop = this.candidate();
    if (!stop?.canAddToItinerary) return;
    this.selected.update((stops) => [...stops, stop]);
    this.choice.set('');
  }

  protected edit(index: number | null): void {
    this.editingIndex.set(index);
    this.editing.set(true);
  }

  protected apply(details: StopDetails): void {
    const index = this.editingIndex();
    const original = this.editingStop();
    const unchanged =
      original &&
      original.name === details.name &&
      original.description === details.description &&
      original.latitude === details.latitude &&
      original.longitude === details.longitude &&
      original.cap === details.cap &&
      original.price === details.price &&
      original.municipality === details.municipality &&
      original.province === details.province &&
      original.sourceUrl === details.sourceUrl;
    if (!unchanged) {
      const stop: EditorStop = {
        id: original?.id ?? `new-${++this.nextTemporaryId}`,
        ...details,
        change: { stopId: original?.change ? original.change.stopId : original?.id, details },
      };
      this.selected.update((stops) =>
        index === null
          ? [...stops, stop]
          : stops.map((previous, current) => (current === index ? stop : previous)),
      );
    }
    this.editing.set(false);
  }

  protected remove(index: number): void {
    this.selected.update((stops) => stops.filter((_, current) => current !== index));
  }

  protected move(index: number, direction: -1 | 1): void {
    const stops = this.selected().slice();
    const next = index + direction;
    if (next < 0 || next >= stops.length) return;
    [stops[index], stops[next]] = [stops[next], stops[index]];
    this.selected.set(stops);
  }

  private searchText(value: string): string {
    return value
      .normalize('NFD')
      .replace(/\p{Diacritic}/gu, '')
      .toLocaleLowerCase('it');
  }
}
