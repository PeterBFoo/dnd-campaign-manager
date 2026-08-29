import { Routes } from '@angular/router';

import { platformAdminGuard } from '@modules/access';
import { AdventureModulesClient } from './api/adventure-modules.client';

export const ADVENTURE_CATALOG_ROUTES: Routes = [
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
];
