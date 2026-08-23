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
  {
    path: '',
    loadChildren: () => import('./modules/characters/characters.routes')
      .then((module) => module.CHARACTERS_ROUTES),
  },
  {
    path: '',
    loadChildren: () => import('./modules/campaigns/campaigns.routes')
      .then((module) => module.CAMPAIGNS_ROUTES),
  },
  {
    path: '',
    loadChildren: () => import('./modules/journal/journal.routes')
      .then((module) => module.JOURNAL_ROUTES),
  },
  { path: '**', redirectTo: '' },
];
