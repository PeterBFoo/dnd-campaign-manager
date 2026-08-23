import { Routes } from '@angular/router';

import { authenticatedGuard } from '@modules/access';
import { CharactersClient } from '@modules/characters';

import { CampaignsClient } from './api/campaigns.client';

export const CAMPAIGNS_ROUTES: Routes = [
  {
    path: 'campaigns',
    providers: [CampaignsClient],
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./campaign-list/campaign-list.page')
      .then((module) => module.CampaignListPage),
  },
  {
    path: 'campaigns/new',
    providers: [CampaignsClient],
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./campaign-create/campaign-create.page')
      .then((module) => module.CampaignCreatePage),
  },
  {
    path: 'campaigns/:campaignId',
    providers: [CampaignsClient, CharactersClient],
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./campaign-detail/campaign-detail.page')
      .then((module) => module.CampaignDetailPage),
  },
];
