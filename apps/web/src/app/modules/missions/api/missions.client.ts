import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiBaseUrl } from '@shared/config/runtime-config';

import { CreateMissionValue, Mission, MissionsResponse, UpdateMissionValue } from './mission.contracts';

@Injectable()
export class MissionsClient {
  private readonly http = inject(HttpClient);

  list(campaignId: string): Observable<MissionsResponse> {
    return this.http.get<MissionsResponse>(this.collectionUrl(campaignId));
  }

  create(campaignId: string, value: CreateMissionValue): Observable<Mission> {
    return this.http.post<Mission>(this.collectionUrl(campaignId), value);
  }

  update(campaignId: string, missionId: string, value: UpdateMissionValue): Observable<Mission> {
    return this.http.put<Mission>(`${this.collectionUrl(campaignId)}/${missionId}`, value);
  }

  setMain(campaignId: string, missionId: string): Observable<Mission> {
    return this.http.put<Mission>(`${this.collectionUrl(campaignId)}/${missionId}/main`, null);
  }

  clearMain(campaignId: string, missionId: string): Observable<void> {
    return this.http.delete<void>(`${this.collectionUrl(campaignId)}/${missionId}/main`);
  }

  delete(campaignId: string, missionId: string): Observable<void> {
    return this.http.delete<void>(`${this.collectionUrl(campaignId)}/${missionId}`);
  }

  private collectionUrl(campaignId: string): string {
    return `${apiBaseUrl()}/api/v1/campaigns/${campaignId}/missions`;
  }
}
