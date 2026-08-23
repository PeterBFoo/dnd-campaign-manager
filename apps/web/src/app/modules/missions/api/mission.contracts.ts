export type MissionStatus = 'active' | 'completed' | 'failed' | 'cancelled';
export type MissionAuthorType = 'dm' | 'player';

export interface Mission {
  id: string;
  campaignId: string;
  title: string;
  description: string | null;
  status: MissionStatus;
  isMain: boolean;
  authorType: MissionAuthorType;
  authorCharacterId: string | null;
  authorDisplayName: string;
  createdAt: string;
  updatedAt: string | null;
  canDelete: boolean;
}

export interface MissionsResponse {
  items: Mission[];
}

export interface CreateMissionValue {
  title: string;
  description: string | null;
  isMain: boolean;
}

export interface UpdateMissionValue {
  title: string;
  description: string | null;
  status: MissionStatus;
}
