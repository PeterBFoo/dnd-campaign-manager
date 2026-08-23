import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiBaseUrl } from '@shared/config/runtime-config';

import { JournalEntriesPage, JournalEntry } from './journal.contracts';

@Injectable()
export class JournalClient {
  private readonly http = inject(HttpClient);

  list(campaignId: string, cursor?: string, limit = 20): Observable<JournalEntriesPage> {
    let params = new HttpParams().set('limit', limit);
    if (cursor) params = params.set('cursor', cursor);
    return this.http.get<JournalEntriesPage>(this.collectionUrl(campaignId), { params });
  }

  create(campaignId: string, content: string): Observable<JournalEntry> {
    return this.http.post<JournalEntry>(this.collectionUrl(campaignId), { content });
  }

  update(campaignId: string, entryId: string, content: string): Observable<JournalEntry> {
    return this.http.put<JournalEntry>(`${this.collectionUrl(campaignId)}/${entryId}`, { content });
  }

  delete(campaignId: string, entryId: string): Observable<void> {
    return this.http.delete<void>(`${this.collectionUrl(campaignId)}/${entryId}`);
  }

  private collectionUrl(campaignId: string): string {
    return `${apiBaseUrl()}/api/v1/campaigns/${campaignId}/journal/entries`;
  }
}
