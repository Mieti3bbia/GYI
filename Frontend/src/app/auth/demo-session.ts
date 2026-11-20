import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of, tap } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class DemoSession {
  private readonly http = inject(HttpClient);
  private editorId: string | null = null;

  get id(): string | null {
    return this.editorId;
  }

  login(username: string, password: string) {
    return this.http
      .post<{ id: string }>('/api/session', { username, password })
      .pipe(tap(({ id }) => (this.editorId = id)));
  }

  restore() {
    return this.http
      .get<{ id: string }>('/api/session')
      .pipe(tap(({ id }) => (this.editorId = id)));
  }

  logout() {
    return this.http.delete<void>('/api/session').pipe(tap(() => (this.editorId = null)));
  }
}

export const editorGuard: CanActivateFn = () => {
  const session = inject(DemoSession);
  const router = inject(Router);
  if (session.id) return true;
  return session.restore().pipe(
    map(() => true),
    catchError(() => of(router.createUrlTree(['/home']))),
  );
};
