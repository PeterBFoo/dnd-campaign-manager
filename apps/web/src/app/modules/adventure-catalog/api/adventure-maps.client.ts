import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { apiBaseUrl } from '@shared/config/runtime-config';
import { AdventureMap, AdventureMapChapter, MapProvenanceInput } from './adventure-maps.contracts';

@Injectable()
export class AdventureMapsClient {
  private readonly http = inject(HttpClient);
  private admin(moduleId: string): string { return `${apiBaseUrl()}/api/v1/admin/adventure-modules/${moduleId}/maps`; }
  private campaign(campaignId: string): string { return `${apiBaseUrl()}/api/v1/campaigns/${campaignId}/adventure/maps`; }
  listAdmin(moduleId: string): Observable<AdventureMap[]> { return this.http.get<AdventureMap[]>(this.admin(moduleId)); }
  chapters(moduleId: string): Observable<AdventureMapChapter[]> { return this.http.get<AdventureMapChapter[]>(`${this.admin(moduleId)}/chapters`); }
  getAdmin(moduleId: string, mapId: string): Observable<AdventureMap> { return this.http.get<AdventureMap>(`${this.admin(moduleId)}/${mapId}`); }
  create(moduleId: string, name: string, description: string): Observable<AdventureMap> { return this.http.post<AdventureMap>(this.admin(moduleId), { name, description }); }
  update(moduleId: string, map: AdventureMap, name: string, description: string): Observable<AdventureMap> { return this.http.put<AdventureMap>(`${this.admin(moduleId)}/${map.id}`, { name, description, expectedVersion: map.version }); }
  delete(moduleId: string, map: AdventureMap): Observable<void> { return this.http.delete<void>(`${this.admin(moduleId)}/${map.id}?expectedVersion=${map.version}`); }
  putImage(moduleId: string, map: AdventureMap, image: File, provenance: MapProvenanceInput): Observable<AdventureMap> {
    const form = new FormData(); form.append('image', image, image.name); form.append('expectedVersion', `${map.version}`);
    form.append('originKind', provenance.originKind); form.append('sourceReference', provenance.sourceReference ?? '');
    form.append('rightsBasis', provenance.rightsBasis); form.append('attribution', provenance.attribution ?? '');
    return this.http.put<AdventureMap>(`${this.admin(moduleId)}/${map.id}/image`, form);
  }
  removeImage(moduleId: string, map: AdventureMap): Observable<AdventureMap> { return this.http.delete<AdventureMap>(`${this.admin(moduleId)}/${map.id}/image?expectedVersion=${map.version}`); }
  setChapter(moduleId: string, map: AdventureMap, chapterId: string, add: boolean): Observable<AdventureMap> {
    const url = `${this.admin(moduleId)}/${map.id}/chapters/${chapterId}?expectedVersion=${map.version}`;
    return add ? this.http.put<AdventureMap>(url, {}) : this.http.delete<AdventureMap>(url);
  }
  imageAdmin(moduleId: string, mapId: string): Observable<Blob> { return this.http.get(`${this.admin(moduleId)}/${mapId}/image`, { responseType: 'blob' }); }
  listCampaign(campaignId: string): Observable<AdventureMap[]> { return this.http.get<AdventureMap[]>(this.campaign(campaignId)); }
  getCampaign(campaignId: string, mapId: string): Observable<AdventureMap> { return this.http.get<AdventureMap>(`${this.campaign(campaignId)}/${mapId}`); }
  imageCampaign(campaignId: string, mapId: string): Observable<Blob> { return this.http.get(`${this.campaign(campaignId)}/${mapId}/image`, { responseType: 'blob' }); }
}
