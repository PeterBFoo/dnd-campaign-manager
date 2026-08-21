import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { SessionStore } from './session.store';

export const authenticatedGuard: CanActivateFn = () => {
  const session = inject(SessionStore);
  return session.authenticated() ? true : inject(Router).createUrlTree(['/']);
};
