export interface CampaignCharacter {
  id: string;
  campaignId: string;
  ownerUserId: string | null;
  ownerDisplayName: string | null;
  name: string;
  armorClass: number;
  initiative: number;
  imageUrl: string;
  isActive: boolean;
  createdAt: string;
}

export interface CharacterOwner {
  userId: string;
  displayName: string;
}

export interface CharacterFormValue {
  name: string;
  armorClass: number;
  initiative: number;
  ownerUserId?: string | null;
  image?: File | null;
  removeImage?: boolean;
}
