import { Routes } from '@angular/router';

import { authenticatedGuard } from '@modules/access';
import { CampaignsClient } from '@modules/campaigns';
import { CharactersClient } from '@modules/characters';

import { CombatClient } from './api/combat.client';

export const COMBAT_ROUTES: Routes = [
  {
    path: 'campaigns/:campaignId/encounters',
    canActivate: [authenticatedGuard],
    providers: [CombatClient, CampaignsClient, CharactersClient],
    loadComponent: () => import('./encounter-page/encounter.page').then((module) => module.EncounterPage),
  },
  {
    path: 'campaigns/:campaignId/encounters/:encounterId',
    canActivate: [authenticatedGuard],
    providers: [CombatClient, CampaignsClient, CharactersClient],
    loadComponent: () => import('./encounter-page/encounter.page').then((module) => module.EncounterPage),
  },
];
