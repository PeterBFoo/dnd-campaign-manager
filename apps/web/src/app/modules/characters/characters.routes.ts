import { Routes } from '@angular/router';

import { authenticatedGuard } from '@modules/access';
import { CampaignsClient } from '@modules/campaigns';

import { CharactersClient } from './api/characters.client';

const providers = [CharactersClient, CampaignsClient];

export const CHARACTERS_ROUTES: Routes = [
  {
    path: 'campaigns/:campaignId/characters',
    providers,
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./character-list/character-list.page').then((module) => module.CharacterListPage),
  },
  {
    path: 'campaigns/:campaignId/characters/new',
    providers,
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./character-form/character-form.page').then((module) => module.CharacterFormPage),
  },
  {
    path: 'campaigns/:campaignId/characters/:characterId/edit',
    providers,
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./character-form/character-form.page').then((module) => module.CharacterFormPage),
  },
];
