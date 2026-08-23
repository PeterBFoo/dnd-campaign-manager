export interface JournalEntry {
  id: string;
  campaignId: string;
  authorCharacterId: string;
  authorCharacterName: string;
  content: string;
  createdAt: string;
  updatedAt: string | null;
  canEdit: boolean;
  canDelete: boolean;
}

export interface JournalEntriesPage {
  items: JournalEntry[];
  nextCursor: string | null;
}
