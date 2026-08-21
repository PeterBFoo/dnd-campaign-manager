import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./shell/home/home.page').then((module) => module.HomePage),
  },
  {
    path: '',
    loadChildren: () => import('./modules/access/access.routes').then((module) => module.ACCESS_ROUTES),
  },
  { path: '**', redirectTo: '' },
];
