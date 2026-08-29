import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiBaseUrl } from '@shared/config/runtime-config';

import { AdventureModule, EditorialProvenanceInput } from './adventure-modules.contracts';

@Injectable()
export class AdventureModulesClient {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${apiBaseUrl()}/api/v1/admin/adventure-modules`;

  list(): Observable<AdventureModule[]> { return this.http.get<AdventureModule[]>(this.baseUrl); }
  get(id: string): Observable<AdventureModule> { return this.http.get<AdventureModule>(`${this.baseUrl}/${id}`); }

  create(name: string, description: string, textProvenance: EditorialProvenanceInput,
    cover: File | null, coverProvenance: EditorialProvenanceInput | null): Observable<AdventureModule> {
    return this.http.post<AdventureModule>(this.baseUrl, this.form(name, description, textProvenance, cover, coverProvenance));
  }

  update(id: string, version: number, name: string, description: string,
    textProvenance: EditorialProvenanceInput, cover: File | null,
    coverProvenance: EditorialProvenanceInput | null, removeCover = false): Observable<AdventureModule> {
    const data = this.form(name, description, textProvenance, cover, coverProvenance);
    data.append('expectedVersion', String(version));
    data.append('removeCover', String(removeCover));
    return this.http.put<AdventureModule>(`${this.baseUrl}/${id}`, data);
  }

  delete(id: string, version: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}?expectedVersion=${version}`);
  }

  coverUrl(id: string): string { return `${this.baseUrl}/${id}/cover`; }
  cover(id: string): Observable<Blob> { return this.http.get(this.coverUrl(id), { responseType: 'blob' }); }

  private form(name: string, description: string, text: EditorialProvenanceInput,
    cover: File | null, coverProvenance: EditorialProvenanceInput | null): FormData {
    const data = new FormData();
    data.append('name', name);
    data.append('description', description);
    data.append('textOriginKind', text.originKind);
    data.append('textSourceReference', text.sourceReference ?? '');
    data.append('textRightsBasis', text.rightsBasis);
    data.append('textAttribution', text.attribution ?? '');
    if (cover) {
      data.append('cover', cover, cover.name);
      data.append('coverOriginKind', coverProvenance?.originKind ?? 'Original');
      data.append('coverSourceReference', coverProvenance?.sourceReference ?? '');
      data.append('coverRightsBasis', coverProvenance?.rightsBasis ?? '');
      data.append('coverAttribution', coverProvenance?.attribution ?? '');
    }
    return data;
  }
}
