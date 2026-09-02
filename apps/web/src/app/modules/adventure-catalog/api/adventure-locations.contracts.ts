export interface LocationMap { id: string; name: string; hasImage: boolean; imageUrl: string | null; width: number | null; height: number | null; }
export interface LocationChapter { id: string; name: string; position: number; }
export interface PointOfInterest { id: string; name: string; description: string | null; x: number | null; y: number | null; createdAt?: string; updatedAt?: string; version?: number; }
export interface LocationPlacement { mapId: string; x: number; y: number; }
export interface AdventureLocation {
  id: string; moduleId: string; name: string; description: string | null; detailMapId: string | null; detailMap: LocationMap | null;
  pointsOfInterest: PointOfInterest[]; placements: LocationPlacement[]; chapters: LocationChapter[];
  createdAt?: string | null; updatedAt?: string | null; version?: number | null;
}
