import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface DraftInput {
  title: string;
  theme: string;
  description: string;
  geographicArea: string;
}

export type ItineraryStatus = 'pending' | 'confirmed';

export interface ItinerarySummary {
  id: string;
  title: string;
  status: ItineraryStatus;
}

export interface FixedStop {
  id: string;
  name: string;
  description: string;
  latitude: number;
  longitude: number;
  cap: string | null;
  price: number;
  municipality: string | null;
  province: string | null;
  sourceUrl: string | null;
}

export interface CatalogStop extends FixedStop {
  canAddToItinerary: boolean;
}

export type StopPosition = Pick<FixedStop, 'name' | 'description' | 'latitude' | 'longitude'>;

export interface StopDetails extends StopPosition {
  cap: string;
  price: number;
  municipality: string;
  province: string;
  sourceUrl: string | null;
}

export interface DraftStopChange {
  stopId?: string | null;
  details?: StopDetails;
}

export interface Segment {
  order: number;
  departureStopId: string;
  arrivalStopId: string;
}

export interface ItineraryDetail extends ItinerarySummary {
  theme: string | null;
  description: string;
  geographicArea: string | null;
  stops: FixedStop[];
  segments: Segment[];
  acceptsEconomicStops: boolean;
  averageTripPrice: number | null;
}

export type ParticipationInput = Pick<ItineraryDetail, 'acceptsEconomicStops' | 'averageTripPrice'>;

@Injectable({ providedIn: 'root' })
export class ItinerariesApi {
  private readonly http = inject(HttpClient);
  private readonly path = '/api/itineraries';

  mine(): Observable<ItinerarySummary[]> {
    return this.http.get<ItinerarySummary[]>(`${this.path}/mine`);
  }

  detail(id: string): Observable<ItineraryDetail> {
    return this.http.get<ItineraryDetail>(`${this.path}/${id}`);
  }

  publicItineraries(): Observable<ItinerarySummary[]> {
    return this.http.get<ItinerarySummary[]>(`${this.path}/public`);
  }

  publicDetail(id: string): Observable<ItineraryDetail> {
    return this.http.get<ItineraryDetail>(`${this.path}/public/${id}`);
  }

  stops(): Observable<CatalogStop[]> {
    return this.http.get<CatalogStop[]>('/api/stops');
  }

  createStop(details: StopDetails): Observable<CatalogStop> {
    return this.http.post<CatalogStop>('/api/stops', details);
  }

  create(input: DraftInput & { stops: DraftStopChange[] }): Observable<ItinerarySummary> {
    return this.http.post<ItinerarySummary>(this.path, input);
  }

  update(id: string, input: DraftInput & { stops: DraftStopChange[] }): Observable<void> {
    return this.http.put<void>(`${this.path}/${id}`, input);
  }

  confirm(id: string): Observable<void> {
    return this.http.put<void>(`${this.path}/${id}/confirm`, null);
  }

  updateParticipation(id: string, input: ParticipationInput): Observable<void> {
    return this.http.put<void>(`${this.path}/${id}/participation`, input);
  }
}
