import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiBaseUrl } from './runtime-config';

export interface InvitationSummary {
  id: string;
  kind: 'platform' | 'campaign';
  recipientEmail: string;
  campaignId: string | null;
  status: 'pending' | 'accepted' | 'expired' | 'revoked';
  deliveryStatus: 'pending' | 'sent' | 'failed' | 'discarded';
  issuedAt: string;
  expiresAt: string;
  lastSentAt: string | null;
}

@Injectable({ providedIn: 'root' })
export class InvitationApiService {
  private readonly http = inject(HttpClient);

  listPlatform(): Observable<InvitationSummary[]> {
    return this.http.get<InvitationSummary[]>(`${apiBaseUrl()}/api/v1/platform/invitations`);
  }

  issuePlatform(email: string): Observable<InvitationSummary> {
    return this.http.post<InvitationSummary>(`${apiBaseUrl()}/api/v1/platform/invitations`, { email });
  }

  resendPlatform(invitationId: string): Observable<InvitationSummary> {
    return this.http.post<InvitationSummary>(
      `${apiBaseUrl()}/api/v1/platform/invitations/${invitationId}/resend`,
      {},
    );
  }

  revokePlatform(invitationId: string): Observable<void> {
    return this.http.delete<void>(`${apiBaseUrl()}/api/v1/platform/invitations/${invitationId}`);
  }

  listCampaign(campaignId: string): Observable<InvitationSummary[]> {
    return this.http.get<InvitationSummary[]>(`${apiBaseUrl()}/api/v1/campaigns/${campaignId}/invitations`);
  }

  issueCampaign(campaignId: string, email: string): Observable<InvitationSummary> {
    return this.http.post<InvitationSummary>(
      `${apiBaseUrl()}/api/v1/campaigns/${campaignId}/invitations`,
      { email },
    );
  }

  resendCampaign(campaignId: string, invitationId: string): Observable<InvitationSummary> {
    return this.http.post<InvitationSummary>(
      `${apiBaseUrl()}/api/v1/campaigns/${campaignId}/invitations/${invitationId}/resend`,
      {},
    );
  }

  revokeCampaign(campaignId: string, invitationId: string): Observable<void> {
    return this.http.delete<void>(
      `${apiBaseUrl()}/api/v1/campaigns/${campaignId}/invitations/${invitationId}`,
    );
  }
}
