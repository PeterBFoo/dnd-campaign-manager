import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiBaseUrl } from '@shared/config/runtime-config';

import {
  EligibleUsersPage,
  InvitationAcceptance,
  InvitationPreview,
  InvitationSummary,
} from './invitation.contracts';

@Injectable()
export class InvitationsClient {
  private readonly http = inject(HttpClient);

  preview(token: string): Observable<InvitationPreview> {
    return this.http.post<InvitationPreview>(`${apiBaseUrl()}/api/v1/invitations/preview`, { token });
  }

  accept(token: string, displayName?: string, password?: string): Observable<InvitationAcceptance> {
    return this.http.post<InvitationAcceptance>(`${apiBaseUrl()}/api/v1/invitations/accept`, {
      token,
      displayName,
      password,
    });
  }

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

  eligibleCampaignUsers(campaignId: string, query?: string): Observable<EligibleUsersPage> {
    const encodedQuery = query ? `?query=${encodeURIComponent(query)}` : '';
    return this.http.get<EligibleUsersPage>(
      `${apiBaseUrl()}/api/v1/campaigns/${campaignId}/eligible-users${encodedQuery}`,
    );
  }

  issueCampaignUser(campaignId: string, recipientUserId: string): Observable<InvitationSummary> {
    return this.http.post<InvitationSummary>(
      `${apiBaseUrl()}/api/v1/campaigns/${campaignId}/invitations`,
      { recipientUserId },
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
