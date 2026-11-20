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
  municipality: string | null;
  province: string | null;
  sourceUrl: string | null;
}

export interface CatalogStop extends FixedStop {
  canAddToItinerary: boolean;
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
}

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

  stops(): Observable<CatalogStop[]> {
    return this.http.get<CatalogStop[]>('/api/stops');
  }

  create(input: DraftInput & { stopIds: string[] }): Observable<ItinerarySummary> {
    return this.http.post<ItinerarySummary>(this.path, input);
  }

  updateStops(id: string, stopIds: string[]): Observable<void> {
    return this.http.put<void>(`${this.path}/${id}/stops`, { stopIds });
  }

  confirm(id: string): Observable<void> {
    return this.http.put<void>(`${this.path}/${id}/confirm`, null);
  }
}
