import { Routes } from '@angular/router';

import { authenticatedGuard } from '@modules/access';
import { CampaignsClient } from '@modules/campaigns';
import { CharactersClient } from '@modules/characters';

import { MissionsClient } from './api/missions.client';

export const MISSIONS_ROUTES: Routes = [
  {
    path: 'campaigns/:campaignId/missions',
    providers: [MissionsClient, CampaignsClient, CharactersClient],
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./mission-page/mission.page').then((module) => module.MissionPage),
  },
];
