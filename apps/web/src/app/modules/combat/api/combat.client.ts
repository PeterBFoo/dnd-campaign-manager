import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiBaseUrl } from '@shared/config/runtime-config';

import {
  ActiveEncounterResponse,
  AddEnemyValue,
  DmEncounter,
  EncountersResponse,
} from './combat.contracts';

@Injectable()
export class CombatClient {
  private readonly http = inject(HttpClient);

  list(campaignId: string): Observable<EncountersResponse> {
    return this.http.get<EncountersResponse>(this.base(campaignId));
  }

  active(campaignId: string): Observable<ActiveEncounterResponse> {
    return this.http.get<ActiveEncounterResponse>(`${this.base(campaignId)}/active`);
  }

  get(campaignId: string, encounterId: string): Observable<DmEncounter> {
    return this.http.get<DmEncounter>(`${this.base(campaignId)}/${encounterId}`);
  }

  create(campaignId: string, name: string): Observable<DmEncounter> {
    return this.http.post<DmEncounter>(this.base(campaignId), { name });
  }

  rename(campaignId: string, encounterId: string, name: string, expectedVersion: number): Observable<DmEncounter> {
    return this.http.put<DmEncounter>(`${this.base(campaignId)}/${encounterId}`, { name, expectedVersion });
  }

  addCharacter(
    campaignId: string,
    encounterId: string,
    characterId: string,
    initiative: number,
    expectedVersion: number,
  ): Observable<DmEncounter> {
    return this.http.post<DmEncounter>(`${this.base(campaignId)}/${encounterId}/characters`, {
      characterId, initiative, expectedVersion,
    });
  }

  addEnemy(campaignId: string, encounterId: string, value: AddEnemyValue): Observable<DmEncounter> {
    return this.http.post<DmEncounter>(`${this.base(campaignId)}/${encounterId}/enemies`, value);
  }

  changeInitiative(
    campaignId: string,
    encounterId: string,
    participantId: string,
    initiative: number,
    expectedVersion: number,
  ): Observable<DmEncounter> {
    return this.http.put<DmEncounter>(
      `${this.base(campaignId)}/${encounterId}/participants/${participantId}/initiative`,
      { initiative, expectedVersion },
    );
  }

  removeParticipant(
    campaignId: string,
    encounterId: string,
    participantId: string,
    expectedVersion: number,
  ): Observable<DmEncounter> {
    const params = new HttpParams().set('expectedVersion', expectedVersion);
    return this.http.delete<DmEncounter>(
      `${this.base(campaignId)}/${encounterId}/participants/${participantId}`,
      { params },
    );
  }

  confirmOrder(
    campaignId: string,
    encounterId: string,
    participantIds: string[],
    expectedVersion: number,
  ): Observable<DmEncounter> {
    return this.http.put<DmEncounter>(`${this.base(campaignId)}/${encounterId}/initiative-order`, {
      participantIds, expectedVersion,
    });
  }

  activate(campaignId: string, encounterId: string, expectedVersion: number): Observable<DmEncounter> {
    return this.http.put<DmEncounter>(`${this.base(campaignId)}/${encounterId}/active`, { expectedVersion });
  }

  advance(campaignId: string, encounterId: string, expectedVersion: number): Observable<DmEncounter> {
    return this.http.post<DmEncounter>(`${this.base(campaignId)}/${encounterId}/turns/advance`, { expectedVersion });
  }

  adjustHitPoints(
    campaignId: string,
    encounterId: string,
    participantId: string,
    memberId: string,
    kind: 'damage' | 'healing',
    amount: number,
    expectedVersion: number,
  ): Observable<DmEncounter> {
    return this.http.post<DmEncounter>(
      `${this.base(campaignId)}/${encounterId}/enemies/${participantId}/members/${memberId}/hit-points`,
      { kind, amount, expectedVersion },
    );
  }

  finish(campaignId: string, encounterId: string, expectedVersion: number): Observable<DmEncounter> {
    return this.http.put<DmEncounter>(`${this.base(campaignId)}/${encounterId}/finished`, { expectedVersion });
  }

  deleteEncounter(campaignId: string, encounterId: string, expectedVersion: number): Observable<void> {
    const params = new HttpParams().set('expectedVersion', expectedVersion);
    return this.http.delete<void>(`${this.base(campaignId)}/${encounterId}`, { params });
  }

  private base(campaignId: string): string {
    return `${apiBaseUrl()}/api/v1/campaigns/${campaignId}/encounters`;
  }
}
