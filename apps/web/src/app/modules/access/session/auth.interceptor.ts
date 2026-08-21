import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { SessionStore } from './session.store';

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const token = inject(SessionStore).accessToken();
  if (!token) {
    return next(request);
  }

  return next(
    request.clone({
      setHeaders: { Authorization: `Bearer ${token}` },
    }),
  );
};
