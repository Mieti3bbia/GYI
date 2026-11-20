import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
} from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  catchError,
  distinctUntilChanged,
  EMPTY,
  finalize,
  map,
  Observable,
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
  StopDetails,
  StopPosition,
} from '../api/itineraries-api';
import { DemoSession } from '../auth/demo-session';
import { RouteMap } from '../map/route-map';
import { EditorStop, StopPicker } from './stop-picker';
import { StopForm } from './stop-form';

function requiredText(control: AbstractControl<string>) {
  return control.value.trim() ? null : { required: true };
}

@Component({
  imports: [ReactiveFormsModule, RouterLink, StopPicker, StopForm, RouteMap],
  templateUrl: './editor-dashboard.html',
  styleUrl: './editor-dashboard.css',
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
  protected readonly saveError = signal<string | null>(null);
  protected readonly detail = signal<ItineraryDetail | null>(null);
  protected readonly confirmError = signal<string | null>(null);
  protected readonly opening = signal(false);
  protected readonly detailError = signal<string | null>(null);
  protected readonly detailId = signal<string | null>(null);
  protected readonly dashboardLink = ['/editor', this.session.id];
  protected readonly catalog = signal<CatalogStop[]>([]);
  protected readonly catalogLoading = signal(false);
  protected readonly catalogError = signal(false);
  protected readonly selectedStops = signal<EditorStop[]>([]);
  protected readonly stopError = signal<string | null>(null);
  protected readonly editing = signal(false);
  protected readonly stopFormOpen = signal(false);
  protected readonly saved = signal(false);
  protected readonly logoutError = signal(false);
  protected readonly insertingStop = signal(false);
  protected readonly catalogStopPreview = signal<FixedStop[]>([]);
  protected readonly catalogStopSaved = signal<CatalogStop | null>(null);
  protected readonly catalogStopError = signal<string | null>(null);
  protected readonly participationOpen = new FormControl(false, { nonNullable: true });
  protected readonly averageTripPrice = new FormControl<number | null>(null);
  protected readonly participationError = signal<string | null>(null);

  constructor() {
    this.route.paramMap
      .pipe(
        map((params) => params.get('id')),
        distinctUntilChanged(),
        switchMap((id) => {
          this.creating.set(false);
          this.insertingStop.set(false);
          this.editing.set(false);
          this.stopFormOpen.set(false);
          this.saved.set(false);
          this.confirmError.set(null);
          this.participationError.set(null);
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
      .subscribe((draft) => {
        this.participationOpen.reset(draft.acceptsEconomicStops ?? false);
        this.averageTripPrice.reset(draft.averageTripPrice ?? null);
        this.detail.set(draft);
      });
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
    this.insertingStop.set(false);
    this.form.reset();
    this.saveError.set(null);
    this.changeStops([]);
    this.saved.set(false);
    this.creating.set(true);
    this.loadCatalog();
  }

  protected save(): void {
    if (this.loading() || this.saving() || this.stopFormOpen()) return;
    this.form.markAllAsTouched();
    this.saveError.set(null);
    this.stopError.set(null);
    if (this.form.invalid) return;

    const id = this.editing() ? this.detailId() : null;
    const fields = this.form.getRawValue();
    const stops = this.selectedStops().map((stop) => stop.change ?? { stopId: stop.id });
    const request: Observable<ItinerarySummary | null> = id
      ? this.api.update(id, { ...fields, stops }).pipe(map(() => null))
      : this.api.create({ ...fields, stops });
    this.saving.set(true);
    request
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: (draft) => {
          if (draft) {
            void this.router.navigate(['/editor', draft.id]);
          } else if (id === this.detailId()) {
            this.editing.set(false);
            this.saved.set(true);
            this.reloadDetail.next();
          }
        },
        error: (response: HttpErrorResponse) => {
          if (id !== this.detailId()) return;
          const errors = response.error?.errors;
          const stopKey = Object.keys(errors ?? {}).find((key) => /^(stopIds|stops)/i.test(key));
          const stopMessage = stopKey ? errors[stopKey]?.[0] : null;
          if (typeof stopMessage === 'string') this.stopError.set(stopMessage);
          let hasFieldError = typeof stopMessage === 'string';
          for (const { key } of this.fields) {
            const message = errors?.[key]?.[0];
            if (typeof message === 'string') {
              this.form.controls[key].setErrors({ server: message });
              hasFieldError = true;
            }
          }
          if (!hasFieldError) this.saveError.set(this.writeError(response));
        },
      });
  }

  protected changeStops(stops: EditorStop[]): void {
    this.selectedStops.set(stops);
    this.stopError.set(null);
  }

  protected editItinerary(draft: ItineraryDetail): void {
    this.form.reset({
      title: draft.title,
      description: draft.description,
      theme: draft.theme ?? '',
      geographicArea: draft.geographicArea ?? '',
    });
    this.changeStops([...new Map(draft.stops.map((stop) => [stop.id, stop])).values()]);
    this.saved.set(false);
    this.saveError.set(null);
    this.editing.set(true);
    this.loadCatalog();
  }

  protected cancelEdit(): void {
    this.creating.set(false);
    this.editing.set(false);
    this.stopFormOpen.set(false);
  }

  protected showCatalogStopForm(): void {
    this.cancelEdit();
    this.insertingStop.set(true);
    this.catalogStopSaved.set(null);
    this.catalogStopError.set(null);
    this.catalogStopPreview.set([]);
  }

  protected showItineraries(): void {
    this.cancelEdit();
    this.insertingStop.set(false);
  }

  protected previewCatalogStop(details: StopPosition | null): void {
    this.catalogStopPreview.set(
      details
        ? [
            {
              ...details,
              id: 'preview',
              name: details.name || 'Nuova tappa',
              cap: null,
              price: 0,
              municipality: null,
              province: null,
              sourceUrl: null,
            },
          ]
        : [],
    );
  }

  protected saveCatalogStop(details: StopDetails): void {
    if (this.saving()) return;
    this.catalogStopError.set(null);
    this.saving.set(true);
    this.api
      .createStop(details)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: (stop) => {
          this.catalogStopSaved.set(stop);
          this.catalogStopPreview.set([stop]);
        },
        error: (response: HttpErrorResponse) => {
          const errors = response.error?.errors as Record<string, string[]> | undefined;
          this.catalogStopError.set(
            response.error?.detail ??
              Object.values(errors ?? {})[0]?.[0] ??
              (response.status === 401
                ? 'Accedi come editor per salvare la tappa.'
                : 'Impossibile salvare la tappa.'),
          );
        },
      });
  }

  protected confirmItinerary(itinerary: ItineraryDetail): void {
    if (this.saving() || this.editing() || itinerary.status !== 'pending') return;
    this.confirmError.set(null);
    this.saved.set(false);
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
        error: (response: HttpErrorResponse) => {
          if (this.detailId() === itinerary.id)
            this.confirmError.set(
              response.error?.detail ?? 'Impossibile confermare l’itinerario. Riprova.',
            );
        },
      });
  }

  protected saveParticipation(itinerary: ItineraryDetail): void {
    if (this.saving()) return;
    this.participationError.set(null);
    const acceptsEconomicStops = this.participationOpen.value;
    const averageTripPrice = acceptsEconomicStops ? this.averageTripPrice.value : null;
    if (
      acceptsEconomicStops &&
      (averageTripPrice === null ||
        !Number.isFinite(averageTripPrice) ||
        averageTripPrice < 0 ||
        averageTripPrice > 9999999.99 ||
        Math.abs(averageTripPrice * 100 - Math.round(averageTripPrice * 100)) > 0.000001)
    ) {
      this.participationError.set(
        'Inserisci un prezzo valido in euro, con al massimo due decimali.',
      );
      return;
    }
    this.saving.set(true);
    this.api
      .updateParticipation(itinerary.id, { acceptsEconomicStops, averageTripPrice })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.saving.set(false)),
      )
      .subscribe({
        next: () => {
          if (this.detailId() !== itinerary.id) return;
          this.saved.set(true);
          this.reloadDetail.next();
        },
        error: (response: HttpErrorResponse) => {
          if (this.detailId() !== itinerary.id) return;
          this.participationError.set(
            response.error?.errors?.averageTripPrice?.[0] ??
              'Impossibile aggiornare la partecipazione.',
          );
        },
      });
  }

  private writeError(response: HttpErrorResponse): string {
    if (response.status === 401 || response.status === 403) {
      return 'Accesso editor necessario. Torna alla home ed effettua il login.';
    }
    if (response.status === 404) return 'L’itinerario non è più modificabile.';
    return 'Impossibile salvare l’itinerario. Riprova.';
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
    this.logoutError.set(false);
    this.session
      .logout()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => void this.router.navigate(['/home']),
        error: () => this.logoutError.set(true),
      });
  }
}
