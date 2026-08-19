import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { AuthenticatedUser } from './auth.service';
import { apiBaseUrl } from './runtime-config';

export interface BootstrapStatus {
  state: 'required' | 'completed';
}

export interface InvitationPreview {
  state: 'valid' | 'invalid' | 'expired' | 'accepted' | 'revoked';
  kind: 'platform' | 'campaign' | null;
  recipientEmail: string | null;
  expiresAt: string | null;
  requiresAuthentication: boolean;
}

export interface InvitationAcceptance {
  user: AuthenticatedUser;
  accessToken: string | null;
  expiresAt: string | null;
  kind: 'platform' | 'campaign';
}

@Injectable({ providedIn: 'root' })
export class IdentityApiService {
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

  previewInvitation(token: string): Observable<InvitationPreview> {
    return this.http.post<InvitationPreview>(`${apiBaseUrl()}/api/v1/invitations/preview`, { token });
  }

  acceptInvitation(token: string, displayName?: string, password?: string): Observable<InvitationAcceptance> {
    return this.http.post<InvitationAcceptance>(`${apiBaseUrl()}/api/v1/invitations/accept`, {
      token,
      displayName,
      password,
    });
  }
}
