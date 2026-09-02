import { EditorialProvenance, EditorialProvenanceInput } from './adventure-modules.contracts';
export interface AdventureChapter { id: string; name: string; description: string | null; position: number; provenance: EditorialProvenance | null; createdAt: string | null; updatedAt: string | null; version: number | null; }
export interface AdventureChapterIndex { indexVersion: number; chapters: AdventureChapter[]; }
export interface AdventureChapterInput { name: string; description: string; provenance: EditorialProvenanceInput; expectedVersion?: number; }
