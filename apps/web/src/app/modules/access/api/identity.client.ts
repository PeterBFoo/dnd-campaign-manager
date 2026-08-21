import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiBaseUrl } from '@shared/config/runtime-config';

import { AuthenticatedUser, BootstrapStatus, SessionResponse } from './identity.contracts';

@Injectable()
export class IdentityClient {
  private readonly http = inject(HttpClient);

  bootstrapStatus(): Observable<BootstrapStatus> {
    return this.http.get<BootstrapStatus>(`${apiBaseUrl()}/api/v1/identity/bootstrap`);
  }

  bootstrap(token: string, email: string, displayName: string, password: string): Observable<AuthenticatedUser> {
    return this.http.post<AuthenticatedUser>(`${apiBaseUrl()}/api/v1/identity/bootstrap`, {
      token,
      email,
      displayName,
      password,
    });
  }

  login(email: string, password: string): Observable<SessionResponse> {
    return this.http.post<SessionResponse>(`${apiBaseUrl()}/api/v1/identity/login`, { email, password });
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${apiBaseUrl()}/api/v1/identity/logout`, {});
  }
}
