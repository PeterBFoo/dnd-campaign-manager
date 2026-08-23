import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiBaseUrl } from '@shared/config/runtime-config';

import { CampaignCharacter, CharacterFormValue, CharacterOwner } from './character.contracts';

@Injectable()
export class CharactersClient {
  private readonly http = inject(HttpClient);

  list(campaignId: string): Observable<CampaignCharacter[]> {
    return this.http.get<CampaignCharacter[]>(this.collectionUrl(campaignId));
  }

  owners(campaignId: string): Observable<CharacterOwner[]> {
    return this.http.get<CharacterOwner[]>(`${this.collectionUrl(campaignId)}/owners`);
  }

  create(campaignId: string, value: CharacterFormValue): Observable<CampaignCharacter> {
    return this.http.post<CampaignCharacter>(this.collectionUrl(campaignId), this.toFormData(value));
  }

  update(
    campaignId: string,
    characterId: string,
    value: CharacterFormValue,
  ): Observable<CampaignCharacter> {
    return this.http.put<CampaignCharacter>(
      `${this.collectionUrl(campaignId)}/${characterId}`,
      this.toFormData(value),
    );
  }

  activate(campaignId: string, characterId: string): Observable<CampaignCharacter> {
    return this.http.put<CampaignCharacter>(
      `${this.collectionUrl(campaignId)}/${characterId}/active`,
      null,
    );
  }

  delete(campaignId: string, characterId: string): Observable<void> {
    return this.http.delete<void>(`${this.collectionUrl(campaignId)}/${characterId}`);
  }

  image(imageUrl: string): Observable<Blob> {
    return this.http.get(`${apiBaseUrl()}${imageUrl}`, { responseType: 'blob' });
  }

  private collectionUrl(campaignId: string): string {
    return `${apiBaseUrl()}/api/v1/campaigns/${campaignId}/characters`;
  }

  private toFormData(value: CharacterFormValue): FormData {
    const body = new FormData();
    body.set('name', value.name);
    body.set('armorClass', String(value.armorClass));
    body.set('initiative', String(value.initiative));
    if (value.ownerUserId) body.set('ownerUserId', value.ownerUserId);
    if (value.image) body.set('image', value.image);
    if (value.removeImage) body.set('removeImage', 'true');
    return body;
  }
}
