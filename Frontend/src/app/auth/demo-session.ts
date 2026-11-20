import { inject, Injectable } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

const editorId = 'f0820e61-db30-478b-a0b1-8870abc7e00e';
const sessionKey = 'gyi.demoEditorId';

@Injectable({ providedIn: 'root' })
export class DemoSession {
  get id(): string | null {
    return sessionStorage.getItem(sessionKey) === editorId ? editorId : null;
  }

  login(username: string, password: string): boolean {
    if (username !== 'editor' || password !== 'editor') {
      return false;
    }
    sessionStorage.setItem(sessionKey, editorId);
    return true;
  }

  logout(): void {
    sessionStorage.removeItem(sessionKey);
  }
}

export const editorGuard: CanActivateFn = () =>
  inject(DemoSession).id ? true : inject(Router).createUrlTree(['/home']);
