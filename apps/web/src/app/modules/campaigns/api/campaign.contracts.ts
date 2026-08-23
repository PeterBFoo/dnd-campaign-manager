export interface CampaignSummary {
  id: string;
  name: string;
  role: 'dm' | 'player';
  adventureModuleId: string | null;
  createdAt: string;
}
