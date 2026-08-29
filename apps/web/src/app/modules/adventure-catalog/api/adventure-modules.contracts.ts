export type EditorialOrigin = 'Original' | 'Licensed' | 'Permission' | 'PublicDomain' | 'FanContentPolicy';

export interface EditorialProvenance {
  originKind: EditorialOrigin;
  sourceReference: string | null;
  rightsBasis: string;
  attribution: string | null;
  verifiedAt: string;
}

export interface AdventureModule {
  id: string;
  name: string;
  description: string | null;
  coverUrl: string | null;
  textProvenance: EditorialProvenance;
  coverProvenance: EditorialProvenance | null;
  createdAt: string;
  updatedAt: string;
  version: number;
}

export interface EditorialProvenanceInput {
  originKind: EditorialOrigin;
  sourceReference?: string;
  rightsBasis: string;
  attribution?: string;
}
