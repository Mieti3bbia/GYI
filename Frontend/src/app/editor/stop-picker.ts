import { Component, computed, input, model, signal } from '@angular/core';
import { FixedStop } from '../api/itineraries-api';

@Component({
  selector: 'app-stop-picker',
  templateUrl: './stop-picker.html',
})
export class StopPicker {
  readonly catalog = input.required<FixedStop[]>();
  readonly selected = model.required<FixedStop[]>();
  readonly error = input<string | null>(null);

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

  protected search(value: string): void {
    this.query.set(value);
    this.choice.set('');
  }

  protected add(): void {
    const stop = this.available().find(({ id }) => id === this.choice());
    if (!stop) return;
    this.selected.update((stops) => [...stops, stop]);
    this.choice.set('');
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
