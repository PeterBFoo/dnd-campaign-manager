import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { SessionStore } from './session.store';

export const platformAdminGuard: CanActivateFn = () => {
  const session = inject(SessionStore);
  return session.user()?.isPlatformAdmin
    ? true
    : inject(Router).createUrlTree(['/']);
};
