import { Routes } from '@angular/router';
import { editorGuard } from './auth/demo-session';
import { EditorDashboard } from './editor/editor-dashboard';
import { Home } from './home/home';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'home' },
  { path: 'home', component: Home, title: 'Accedi | GYI' },
  {
    path: 'editor/:id',
    component: EditorDashboard,
    canActivate: [editorGuard],
    title: 'I miei itinerari | GYI',
  },
];
