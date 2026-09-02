import { Routes } from '@angular/router';

import { authenticatedGuard, platformAdminGuard } from '@modules/access';
import { AdventureModulesClient } from './api/adventure-modules.client';
import { AdventureChaptersClient } from './api/adventure-chapters.client';
import { AdventureMapsClient } from './api/adventure-maps.client';
import { AdventureLocationsClient } from './api/adventure-locations.client';

export const ADVENTURE_CATALOG_ROUTES: Routes = [
  { path: 'admin/adventure-modules/:moduleId/locations', providers: [AdventureLocationsClient, AdventureMapsClient, AdventureChaptersClient], canActivate: [platformAdminGuard], loadComponent: () => import('./adventure-locations-admin.page').then(m => m.AdventureLocationsAdminPage) },
  { path: 'campaigns/:campaignId/adventure/locations', providers: [AdventureLocationsClient, AdventureMapsClient], canActivate: [authenticatedGuard], loadComponent: () => import('./campaign-adventure-locations.page').then(m => m.CampaignAdventureLocationsPage) },
  { path: 'admin/adventure-modules/:moduleId/chapters', providers: [AdventureChaptersClient], canActivate: [platformAdminGuard], loadComponent: () => import('./adventure-chapters.page').then(m => m.AdventureChaptersPage) },
  { path: 'campaigns/:campaignId/adventure/chapters', providers: [AdventureChaptersClient], canActivate: [authenticatedGuard], loadComponent: () => import('./adventure-chapters.page').then(m => m.AdventureChaptersPage) },
  {
    path: 'admin/adventure-modules/:moduleId/maps',
    providers: [AdventureMapsClient],
    canActivate: [platformAdminGuard],
    loadComponent: () => import('./adventure-maps-admin.page').then((module) => module.AdventureMapsAdminPage),
  },
  {
    path: 'admin/adventure-modules',
    pathMatch: 'full',
    providers: [AdventureModulesClient],
    canActivate: [platformAdminGuard],
    loadComponent: () => import('./adventure-modules.page').then((module) => module.AdventureModulesPage),
  },
  {
    path: 'admin/adventure-modules/new',
    providers: [AdventureModulesClient],
    canActivate: [platformAdminGuard],
    loadComponent: () => import('./adventure-module-editor.page').then((module) => module.AdventureModuleEditorPage),
  },
  {
    path: 'admin/adventure-modules/:moduleId/edit',
    providers: [AdventureModulesClient],
    canActivate: [platformAdminGuard],
    loadComponent: () => import('./adventure-module-editor.page').then((module) => module.AdventureModuleEditorPage),
  },
  {
    path: 'admin/adventure-modules/:moduleId',
    providers: [AdventureModulesClient],
    canActivate: [platformAdminGuard],
    loadComponent: () => import('./adventure-module-detail.page').then((module) => module.AdventureModuleDetailPage),
  },
  {
    path: 'campaigns/:campaignId/adventure/maps',
    providers: [AdventureMapsClient],
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./campaign-adventure-maps.page').then((module) => module.CampaignAdventureMapsPage),
  },
];
