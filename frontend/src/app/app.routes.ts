import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./components/home/home').then(m => m.Home),
  },
  {
    path: 'setup',
    loadComponent: () => import('./components/setup/setup').then(m => m.Setup),
  },
];
