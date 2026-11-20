import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { catchError, finalize, of, Subject, switchMap } from 'rxjs';
import {
  FixedStop,
  ItinerariesApi,
  ItineraryDetail,
  ItinerarySummary,
} from '../api/itineraries-api';
import { DemoSession } from '../auth/demo-session';
import { RouteMap } from '../map/route-map';

@Component({
  imports: [ReactiveFormsModule, RouteMap],
  templateUrl: './home.html',
  styleUrl: './home.css',
})
export class Home {
  private readonly session = inject(DemoSession);
  private readonly api = inject(ItinerariesApi);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly detailRequests = new Subject<string>();

  protected readonly form = inject(NonNullableFormBuilder).group({ username: '', password: '' });
  protected readonly failed = signal(false);
  protected readonly signingIn = signal(false);
  protected readonly itineraries = signal<ItinerarySummary[]>([]);
  protected readonly itinerary = signal<ItineraryDetail | null>(null);
  protected readonly selectedId = signal('');
  protected readonly activeStopId = signal<string | null>(null);
  protected readonly loadingCatalog = signal(false);
  protected readonly catalogError = signal(false);
  protected readonly loadingDetail = signal(false);
  protected readonly detailError = signal(false);
  protected readonly emptyStops: readonly FixedStop[] = [];
  protected readonly euros = new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' });

  constructor() {
    this.detailRequests
      .pipe(
        switchMap((id) => this.api.publicDetail(id).pipe(catchError(() => of(null)))),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((detail) => {
        const confirmed = detail?.status === 'confirmed';
        this.loadingDetail.set(false);
        this.detailError.set(!confirmed);
        this.itinerary.set(confirmed ? detail : null);
      });

    this.loadCatalog();
  }

  protected loadCatalog(): void {
    if (this.loadingCatalog()) return;
    this.loadingCatalog.set(true);
    this.catalogError.set(false);
    this.api
      .publicItineraries()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loadingCatalog.set(false)),
      )
      .subscribe({
        next: (itineraries) => {
          const confirmed = itineraries.filter(({ status }) => status === 'confirmed');
          this.itineraries.set(confirmed);
          if (confirmed.length) this.selectItinerary(confirmed[0].id);
        },
        error: () => this.catalogError.set(true),
      });
  }

  protected selectItinerary(id: string): void {
    if (!this.itineraries().some((itinerary) => itinerary.id === id)) return;
    this.selectedId.set(id);
    this.itinerary.set(null);
    this.activeStopId.set(null);
    this.detailError.set(false);
    this.loadingDetail.set(true);
    this.detailRequests.next(id);
  }

  protected login(): void {
    if (this.signingIn()) return;
    const { username, password } = this.form.getRawValue();
    this.failed.set(false);
    this.signingIn.set(true);
    this.session
      .login(username, password)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.signingIn.set(false)),
      )
      .subscribe({
        next: ({ id }) => void this.router.navigate(['/editor', id]),
        error: () => this.failed.set(true),
      });
  }
}
