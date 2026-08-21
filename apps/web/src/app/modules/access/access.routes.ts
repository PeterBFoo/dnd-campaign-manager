import { Routes } from '@angular/router';

import { InvitationsClient } from './api/invitations.client';
import { authenticatedGuard } from './session/authenticated.guard';
import { platformAdminGuard } from './session/platform-admin.guard';

export const ACCESS_ROUTES: Routes = [
  {
    path: 'bootstrap',
    loadComponent: () => import('./bootstrap/bootstrap.page').then((module) => module.BootstrapPage),
  },
  {
    path: 'accept-invitation',
    providers: [InvitationsClient],
    loadComponent: () => import('./invitation-acceptance/invitation-acceptance.page')
      .then((module) => module.InvitationAcceptancePage),
  },
  {
    path: 'admin/invitations',
    providers: [InvitationsClient],
    canActivate: [platformAdminGuard],
    loadComponent: () => import('./invitation-management/platform-invitations.page')
      .then((module) => module.PlatformInvitationsPage),
  },
  {
    path: 'campaigns/:campaignId/invitations',
    providers: [InvitationsClient],
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./invitation-management/campaign-invitations.page')
      .then((module) => module.CampaignInvitationsPage),
  },
];
