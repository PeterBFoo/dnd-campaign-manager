import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { apiBaseUrl } from '@shared/config/runtime-config';
import { AdventureLocation } from './adventure-locations.contracts';

@Injectable()
export class AdventureLocationsClient {
  private readonly http = inject(HttpClient);
  private admin(moduleId: string): string { return `${apiBaseUrl()}/api/v1/admin/adventure-modules/${moduleId}/locations`; }
  private campaign(campaignId: string): string { return `${apiBaseUrl()}/api/v1/campaigns/${campaignId}/adventure/locations`; }
  listAdmin(moduleId: string): Observable<AdventureLocation[]> { return this.http.get<AdventureLocation[]>(this.admin(moduleId)); }
  getAdmin(moduleId: string, id: string): Observable<AdventureLocation> { return this.http.get<AdventureLocation>(`${this.admin(moduleId)}/${id}`); }
  create(moduleId: string, name: string, description: string): Observable<AdventureLocation> { return this.http.post<AdventureLocation>(this.admin(moduleId), { name, description }); }
  update(moduleId: string, item: AdventureLocation, name: string, description: string): Observable<AdventureLocation> { return this.http.put<AdventureLocation>(`${this.admin(moduleId)}/${item.id}`, { name, description, expectedVersion: item.version }); }
  delete(moduleId: string, item: AdventureLocation): Observable<void> { return this.http.delete<void>(`${this.admin(moduleId)}/${item.id}?expectedVersion=${item.version}`); }
  setDetailMap(moduleId: string, item: AdventureLocation, mapId: string | null): Observable<AdventureLocation> {
    const url = `${this.admin(moduleId)}/${item.id}/detail-map`;
    return mapId ? this.http.put<AdventureLocation>(url, { mapId, expectedVersion: item.version }) : this.http.delete<AdventureLocation>(`${url}?expectedVersion=${item.version}`);
  }
  createPoint(moduleId: string, item: AdventureLocation, input: { name: string; description: string; x: number | null; y: number | null }): Observable<AdventureLocation> { return this.http.post<AdventureLocation>(`${this.admin(moduleId)}/${item.id}/points-of-interest`, { ...input, expectedVersion: item.version }); }
  updatePoint(moduleId: string, item: AdventureLocation, pointId: string, input: { name: string; description: string; x: number | null; y: number | null }): Observable<AdventureLocation> { return this.http.put<AdventureLocation>(`${this.admin(moduleId)}/${item.id}/points-of-interest/${pointId}`, { ...input, expectedVersion: item.version }); }
  deletePoint(moduleId: string, item: AdventureLocation, pointId: string): Observable<AdventureLocation> { return this.http.delete<AdventureLocation>(`${this.admin(moduleId)}/${item.id}/points-of-interest/${pointId}?expectedVersion=${item.version}`); }
  setChapter(moduleId: string, item: AdventureLocation, chapterId: string, add: boolean): Observable<AdventureLocation> { const url = `${this.admin(moduleId)}/${item.id}/chapters/${chapterId}?expectedVersion=${item.version}`; return add ? this.http.put<AdventureLocation>(url, {}) : this.http.delete<AdventureLocation>(url); }
  setPlacement(moduleId: string, item: AdventureLocation, mapId: string, x: number, y: number): Observable<AdventureLocation> { return this.http.put<AdventureLocation>(`${this.admin(moduleId)}/${item.id}/placements/${mapId}`, { x, y, expectedVersion: item.version }); }
  removePlacement(moduleId: string, item: AdventureLocation, mapId: string): Observable<AdventureLocation> { return this.http.delete<AdventureLocation>(`${this.admin(moduleId)}/${item.id}/placements/${mapId}?expectedVersion=${item.version}`); }
  listCampaign(campaignId: string): Observable<AdventureLocation[]> { return this.http.get<AdventureLocation[]>(this.campaign(campaignId)); }
  getCampaign(campaignId: string, id: string): Observable<AdventureLocation> { return this.http.get<AdventureLocation>(`${this.campaign(campaignId)}/${id}`); }
}
