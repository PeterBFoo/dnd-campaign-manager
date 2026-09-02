import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { apiBaseUrl } from '@shared/config/runtime-config';
import { AdventureChapter, AdventureChapterIndex, AdventureChapterInput } from './adventure-chapters.contracts';
@Injectable() export class AdventureChaptersClient {
  private readonly http = inject(HttpClient);
  adminList(moduleId: string) { return this.http.get<AdventureChapterIndex>(`${apiBaseUrl()}/api/v1/admin/adventure-modules/${moduleId}/chapters`); }
  create(moduleId: string, input: AdventureChapterInput) { return this.http.post<AdventureChapter>(`${apiBaseUrl()}/api/v1/admin/adventure-modules/${moduleId}/chapters`, input); }
  update(moduleId: string, chapterId: string, input: AdventureChapterInput) { return this.http.put<AdventureChapter>(`${apiBaseUrl()}/api/v1/admin/adventure-modules/${moduleId}/chapters/${chapterId}`, input); }
  delete(moduleId: string, chapter: AdventureChapter) { return this.http.delete<void>(`${apiBaseUrl()}/api/v1/admin/adventure-modules/${moduleId}/chapters/${chapter.id}?expectedVersion=${chapter.version}`); }
  reorder(moduleId: string, expectedIndexVersion: number, chapterIds: string[]) { return this.http.put<AdventureChapterIndex>(`${apiBaseUrl()}/api/v1/admin/adventure-modules/${moduleId}/chapters/order`, { expectedIndexVersion, chapterIds }); }
  campaignList(campaignId: string) { return this.http.get<AdventureChapterIndex>(`${apiBaseUrl()}/api/v1/campaigns/${campaignId}/adventure/chapters`); }
  campaignGet(campaignId: string, chapterId: string) { return this.http.get<AdventureChapter>(`${apiBaseUrl()}/api/v1/campaigns/${campaignId}/adventure/chapters/${chapterId}`); }
}
