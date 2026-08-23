import { Routes } from '@angular/router';

import { authenticatedGuard } from '@modules/access';
import { CampaignsClient } from '@modules/campaigns';
import { CharactersClient } from '@modules/characters';

import { JournalClient } from './api/journal.client';

export const JOURNAL_ROUTES: Routes = [
  {
    path: 'campaigns/:campaignId/journal',
    providers: [JournalClient, CampaignsClient, CharactersClient],
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./journal-page/journal.page').then((module) => module.JournalPage),
  },
];
