import { Routes } from '@angular/router';
import { editorGuard } from './auth/demo-session';
import { EditorDashboard } from './editor/editor-dashboard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'home' },
  {
    path: 'home',
    loadComponent: () => import('./home/home').then(({ Home }) => Home),
    title: 'Esplora gli itinerari | GYI',
  },
  {
    path: 'editor/:id',
    component: EditorDashboard,
    canActivate: [editorGuard],
    title: 'I miei itinerari | GYI',
  },
];
