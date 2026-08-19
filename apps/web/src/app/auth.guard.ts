import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';

export const authenticatedGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.authenticated() ? true : inject(Router).createUrlTree(['/']);
};

export const platformAdminGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.user()?.isPlatformAdmin
    ? true
    : inject(Router).createUrlTree(['/']);
};
