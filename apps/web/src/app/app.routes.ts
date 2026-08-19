import { Routes } from '@angular/router';
import { authenticatedGuard, platformAdminGuard } from './auth.guard';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./landing.component').then((module) => module.LandingComponent),
  },
  {
    path: 'bootstrap',
    loadComponent: () => import('./bootstrap.component').then((module) => module.BootstrapComponent),
  },
  {
    path: 'accept-invitation',
    loadComponent: () => import('./accept-invitation.component').then((module) => module.AcceptInvitationComponent),
  },
  {
    path: 'admin/invitations',
    canActivate: [platformAdminGuard],
    loadComponent: () => import('./admin-invitations.component').then((module) => module.AdminInvitationsComponent),
  },
  {
    path: 'campaigns/:campaignId/invitations',
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./campaign-invitations.component').then((module) => module.CampaignInvitationsComponent),
  },
  { path: '**', redirectTo: '' },
];
