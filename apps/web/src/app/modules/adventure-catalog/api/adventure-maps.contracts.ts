export interface AdventureMapChapter { id: string; name: string; position: number; }
export interface AdventureMap {
  id: string; moduleId: string; name: string; description: string | null; hasImage: boolean;
  imageUrl: string | null; width: number | null; height: number | null;
  imageProvenance?: { originKind: string; sourceReference: string | null; rightsBasis: string; attribution: string | null; verifiedAt: string } | null;
  chapters: AdventureMapChapter[]; createdAt: string; updatedAt: string; version: number;
}
export interface MapProvenanceInput { originKind: string; sourceReference?: string; rightsBasis: string; attribution?: string; }
