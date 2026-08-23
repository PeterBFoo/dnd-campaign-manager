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

  create(name: string): Observable<CampaignSummary> {
    return this.http.post<CampaignSummary>(`${apiBaseUrl()}/api/v1/campaigns`, { name });
  }

  get(campaignId: string): Observable<CampaignSummary> {
    return this.http.get<CampaignSummary>(`${apiBaseUrl()}/api/v1/campaigns/${campaignId}`);
  }
}
