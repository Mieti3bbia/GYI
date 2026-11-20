import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface DraftInput {
  title: string;
  theme: string;
  description: string;
  geographicArea: string;
}

export interface DraftSummary {
  id: string;
  title: string;
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

export interface Segment {
  order: number;
  departureStopId: string;
  arrivalStopId: string;
}

export interface Draft extends DraftSummary {
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

  mine(): Observable<DraftSummary[]> {
    return this.http.get<DraftSummary[]>(`${this.path}/mine`);
  }

  detail(id: string): Observable<Draft> {
    return this.http.get<Draft>(`${this.path}/${id}`);
  }

  fixedStops(): Observable<FixedStop[]> {
    return this.http.get<FixedStop[]>('/api/stops/fixed');
  }

  create(input: DraftInput & { stopIds: string[] }): Observable<DraftSummary> {
    return this.http.post<DraftSummary>(this.path, input);
  }

  updateStops(id: string, stopIds: string[]): Observable<void> {
    return this.http.put<void>(`${this.path}/${id}/stops`, { stopIds });
  }
}
