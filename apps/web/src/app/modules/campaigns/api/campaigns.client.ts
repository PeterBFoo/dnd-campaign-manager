import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiBaseUrl } from '@shared/config/runtime-config';

import { CampaignSummary } from './campaign.contracts';

@Injectable()
export class CampaignsClient {
  private readonly http = inject(HttpClient);

  list(): Observable<CampaignSummary[]> {
    return this.http.get<CampaignSummary[]>(`${apiBaseUrl()}/api/v1/campaigns`);
  }

  create(name: string, adventureModuleId: string | null = null): Observable<CampaignSummary> {
    return this.http.post<CampaignSummary>(`${apiBaseUrl()}/api/v1/campaigns`, { name, adventureModuleId });
  }

  get(campaignId: string): Observable<CampaignSummary> {
    return this.http.get<CampaignSummary>(`${apiBaseUrl()}/api/v1/campaigns/${campaignId}`);
  }

  delete(campaignId: string): Observable<void> {
    return this.http.delete<void>(`${apiBaseUrl()}/api/v1/campaigns/${campaignId}`);
  }

  assignModule(
    campaignId: string,
    adventureModuleId: string,
    expectedVersion: number,
  ): Observable<CampaignSummary> {
    return this.http.put<CampaignSummary>(
      `${apiBaseUrl()}/api/v1/campaigns/${campaignId}/adventure-module`,
      { adventureModuleId, expectedVersion },
    );
  }

  removeModule(campaignId: string, expectedVersion: number): Observable<CampaignSummary> {
    return this.http.delete<CampaignSummary>(
      `${apiBaseUrl()}/api/v1/campaigns/${campaignId}/adventure-module?expectedVersion=${expectedVersion}`,
    );
  }
}
