import { Routes } from '@angular/router';
import { editorGuard } from './auth/demo-session';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'home' },
  {
    path: 'home',
    loadComponent: () => import('./home/home').then(({ Home }) => Home),
  },
  {
    path: 'editor/:id',
    loadComponent: () =>
      import('./editor/editor-dashboard').then(({ EditorDashboard }) => EditorDashboard),
    canActivate: [editorGuard],
  },
];
