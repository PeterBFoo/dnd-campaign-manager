import type { AdventureModuleOption } from '@modules/adventure-catalog';

export type CampaignAdventureModule = AdventureModuleOption;

export interface CampaignSummary {
  id: string;
  name: string;
  role: 'dm' | 'player';
  adventureModuleId: string | null;
  createdAt: string;
  adventureModule: CampaignAdventureModule | null;
  version: number;
}
