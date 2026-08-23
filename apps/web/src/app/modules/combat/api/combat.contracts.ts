export type EncounterStatus = 'draft' | 'active' | 'finished';
export type ParticipantKind = 'character' | 'enemy';

export interface EncounterSummary {
  id: string;
  name: string;
  status: EncounterStatus;
  participantCount: number;
  tiesResolved: boolean;
  round: number | null;
  currentParticipantName: string | null;
  version: number;
  createdAt: string;
  activatedAt: string | null;
  finishedAt: string | null;
}

export interface EncountersResponse {
  items: EncounterSummary[];
}

export interface DmParticipant {
  id: string;
  characterId: string | null;
  name: string;
  kind: ParticipantKind;
  armorClass: number;
  initiative: number;
  orderPosition: number;
  quantity: number;
  members: DmEnemyMember[];
  isCurrentTurn: boolean;
}

export interface DmEnemyMember {
  id: string;
  ordinal: number;
  currentHitPoints: number;
  maximumHitPoints: number;
}

export interface DmEncounter {
  id: string;
  campaignId: string;
  name: string;
  status: EncounterStatus;
  round: number | null;
  currentParticipantId: string | null;
  tiesResolved: boolean;
  version: number;
  createdAt: string;
  activatedAt: string | null;
  finishedAt: string | null;
  participants: DmParticipant[];
}

export interface ActiveParticipant {
  name: string;
  kind: ParticipantKind;
  initiative: number;
  orderPosition: number;
  quantity: number;
  isCurrentTurn: boolean;
}

export interface ActiveEncounter {
  id: string;
  name: string;
  round: number;
  currentParticipantName: string;
  participants: ActiveParticipant[];
}

export interface ActiveEncounterResponse {
  encounter: ActiveEncounter | null;
}

export interface AddEnemyValue {
  name: string;
  initiative: number;
  armorClass: number;
  maximumHitPoints: number;
  quantity: number;
  expectedVersion: number;
}
