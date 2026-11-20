import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  catchError,
  distinctUntilChanged,
  EMPTY,
  finalize,
  map,
  startWith,
  Subject,
  switchMap,
} from 'rxjs';
import {
  CatalogStop,
  DraftInput,
  FixedStop,
  ItinerariesApi,
  ItineraryDetail,
  ItinerarySummary,
} from '../api/itineraries-api';
import { DemoSession } from '../auth/demo-session';
import { StopPicker } from './stop-picker';

function requiredText(control: AbstractControl<string>) {
  return control.value.trim() ? null : { required: true };
}

@Component({
  imports: [ReactiveFormsModule, RouterLink, StopPicker],
  templateUrl: './editor-dashboard.html',
})
export class EditorDashboard {
  private readonly api = inject(ItinerariesApi);
  private readonly session = inject(DemoSession);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  private readonly reloadDetail = new Subject<void>();

  protected readonly fields = [
    { key: 'title', label: 'Titolo' },
    { key: 'theme', label: 'Tema' },
    { key: 'description', label: 'Descrizione' },
    { key: 'geographicArea', label: 'Area geografica' },
  ] as const;
  protected readonly form = inject(NonNullableFormBuilder).group({
    title: ['', requiredText],
    theme: ['', requiredText],
    description: ['', requiredText],
    geographicArea: ['', requiredText],
  });
  protected readonly itineraries = signal<ItinerarySummary[]>([]);
  protected readonly loading = signal(false);
  protected readonly listError = signal(false);
  protected readonly creating = signal(false);
  protected readonly saving = signal(false);
  protected readonly saveError = signal(false);
  protected readonly detail = signal<ItineraryDetail | null>(null);
  protected readonly confirmError = signal(false);
  protected readonly opening = signal(false);
  protected readonly detailError = signal<string | null>(null);
  protected readonly detailId = signal<string | null>(null);
  protected readonly dashboardLink = ['/editor', this.session.id];
  protected readonly catalog = signal<CatalogStop[]>([]);
  protected readonly catalogLoading = signal(false);
  protected readonly catalogError = signal(false);
  protected readonly selectedStops = signal<FixedStop[]>([]);
  protected readonly stopError = signal<string | null>(null);
  protected readonly editingStops = signal(false);
  protected readonly stopsSaved = signal(false);

  constructor() {
    this.route.paramMap
      .pipe(
        map((params) => params.get('id')),
        distinctUntilChanged(),
        switchMap((id) => {
          this.creating.set(false);
          this.editingStops.set(false);
          this.stopsSaved.set(false);
          this.confirmError.set(false);
          this.detail.set(null);
          this.detailError.set(null);
          if (id === null || id === this.session.id) {
            this.detailId.set(null);
            this.loadItineraries();
            return EMPTY;
          }

          this.detailId.set(id);
          return this.reloadDetail.pipe(
            startWith(undefined),
            switchMap(() => {
              this.detail.set(null);
              this.detailError.set(null);
              this.opening.set(true);
              return this.api.detail(id).pipe(
                catchError((error: HttpErrorResponse) => {
                  this.detailError.set(
                    error.status === 404
                      ? 'Itinerario non trovato.'
                      : 'Impossibile aprire l’itinerario.',
                  );
                  return EMPTY;
                }),
                finalize(() => this.opening.set(false)),
              );
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((draft) => this.detail.set(draft));
  }

  protected loadCatalog(): void {
    if (this.catalogLoading()) return;
    this.catalogLoading.set(true);
    this.catalogError.set(false);
    this.api
      .stops()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.catalogLoading.set(false)),
      )
      .subscribe({
        next: (stops) => this.catalog.set(stops),
        error: () => this.catalogError.set(true),
      });
  }

  protected loadItineraries(): void {
    if (this.loading()) return;
    this.loading.set(true);
    this.listError.set(false);
    this.api
      .mine()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false)),
      )
      .subscribe({
        next: (itineraries) => this.itineraries.set(itineraries),
        error: () => this.listError.set(true),
      });
  }

  protected newDraft(): void {
    this.form.reset();
    this.saveError.set(false);
    this.changeStops([]);
    this.stopsSaved.set(false);
    this.creating.set(true);
    this.loadCatalog();
  }

  protected save(): void {
    if (this.loading() || this.saving() || this.catalogLoading()) return;
    this.form.markAllAsTouched();
    this.saveError.set(false);
    this.stopError.set(null);
    if (this.form.invalid) return;

    this.saving.set(true);
    this.api
      .create({ ...this.form.getRawValue(), stopIds: this.getStopIds() })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: (draft) => {
          void this.router.navigate(['/editor', draft.id]);
        },
        error: (response: HttpErrorResponse) => {
          const errors = response.error?.errors;
          let hasFieldError = this.showStopError(response);
          for (const { key } of this.fields) {
            const message = errors?.[key]?.[0];
            if (typeof message === 'string') {
              this.form.controls[key].setErrors({ server: message });
              hasFieldError = true;
            }
          }
          this.saveError.set(!hasFieldError);
        },
      });
  }

  protected changeStops(stops: FixedStop[]): void {
    this.selectedStops.set(stops);
    this.stopError.set(null);
  }

  protected editStops(draft: ItineraryDetail): void {
    this.changeStops([...new Map(draft.stops.map((stop) => [stop.id, stop])).values()]);
    this.stopsSaved.set(false);
    this.saveError.set(false);
    this.editingStops.set(true);
    this.loadCatalog();
  }

  protected saveStops(id: string): void {
    if (this.saving() || this.catalogLoading()) return;
    this.saveError.set(false);
    this.stopError.set(null);

    this.saving.set(true);
    this.api
      .updateStops(id, this.getStopIds())
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          if (this.detailId() !== id) return;
          this.editingStops.set(false);
          this.stopsSaved.set(true);
          this.reloadDetail.next();
        },
        error: (response: HttpErrorResponse) => {
          if (this.detailId() === id) this.saveError.set(!this.showStopError(response));
        },
      });
  }

  protected confirmItinerary(itinerary: ItineraryDetail): void {
    if (this.saving() || this.editingStops() || itinerary.status !== 'pending') return;
    this.confirmError.set(false);
    this.stopsSaved.set(false);
    this.saving.set(true);

    this.api
      .confirm(itinerary.id)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          if (this.detailId() === itinerary.id) this.reloadDetail.next();
        },
        error: () => {
          if (this.detailId() === itinerary.id) this.confirmError.set(true);
        },
      });
  }

  private getStopIds(): string[] {
    return this.selectedStops().map((stop) => stop.id);
  }

  private showStopError(response: HttpErrorResponse): boolean {
    const message = response.error?.errors?.stopIds?.[0];
    if (typeof message !== 'string') return false;
    this.stopError.set(message);
    return true;
  }

  protected fieldError(key: keyof DraftInput): string | null {
    const control = this.form.controls[key];
    if (!control.touched) return null;
    return (
      control.getError('server') ?? (control.hasError('required') ? 'Campo obbligatorio.' : null)
    );
  }

  protected retryDetail(): void {
    this.reloadDetail.next();
  }

  protected logout(): void {
    this.session.logout();
    void this.router.navigate(['/home']);
  }
}
