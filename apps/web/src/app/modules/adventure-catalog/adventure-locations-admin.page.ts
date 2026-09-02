import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '@shared/http/problem-details';
import { AdventureChaptersClient } from './api/adventure-chapters.client';
import { AdventureChapter } from './api/adventure-chapters.contracts';
import { AdventureLocationsClient } from './api/adventure-locations.client';
import { AdventureLocation, PointOfInterest } from './api/adventure-locations.contracts';
import { AdventureMapsClient } from './api/adventure-maps.client';
import { AdventureMap } from './api/adventure-maps.contracts';

@Component({ selector: 'dnd-adventure-locations-admin', imports: [DecimalPipe, FormsModule, RouterLink], templateUrl: './adventure-locations-admin.page.html', styleUrl: './adventure-locations-admin.page.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class AdventureLocationsAdminPage implements OnInit {
  private readonly client = inject(AdventureLocationsClient);
  private readonly mapsClient = inject(AdventureMapsClient);
  private readonly chaptersClient = inject(AdventureChaptersClient);
  readonly moduleId = inject(ActivatedRoute).snapshot.paramMap.get('moduleId') ?? '';
  readonly locations = signal<AdventureLocation[]>([]); readonly maps = signal<AdventureMap[]>([]); readonly chapters = signal<AdventureChapter[]>([]); readonly selected = signal<AdventureLocation | null>(null);
  readonly loading = signal(true); readonly saving = signal(false); readonly error = signal<string | null>(null); readonly imageSource = signal<string | null>(null); readonly activeMapImage = signal<string | null>(null); readonly activeMapId = signal<string | null>(null);
  name = ''; description = ''; poiName = ''; poiDescription = ''; poiX: number | null = null; poiY: number | null = null; editingPointId: string | null = null; placementMapId = ''; placementX = 0; placementY = 0;

  ngOnInit(): void { this.reload(); this.mapsClient.listAdmin(this.moduleId).subscribe({ next: value => this.maps.set(value) }); this.chaptersClient.adminList(this.moduleId).subscribe({ next: value => this.chapters.set(value.chapters) }); }
  reload(): void { this.loading.set(true); this.client.listAdmin(this.moduleId).pipe(finalize(() => this.loading.set(false))).subscribe({ next: value => this.locations.set(value), error: e => this.error.set(apiErrorMessage(e, 'No se han podido cargar las localizaciones.')) }); }
  choose(item: AdventureLocation): void { this.selected.set(item); this.name = item.name; this.description = item.description ?? ''; this.resetPoint(); this.activeMapId.set(null); this.activeMapImage.set(null); if (item.detailMap?.hasImage) this.loadImage(item.detailMap.id); }
  newLocation(): void { this.selected.set(null); this.name = ''; this.description = ''; this.resetPoint(); this.activeMapId.set(null); this.activeMapImage.set(null); }
  save(): void { this.saving.set(true); this.error.set(null); const item = this.selected(); const request = item ? this.client.update(this.moduleId, item, this.name, this.description) : this.client.create(this.moduleId, this.name, this.description); request.pipe(finalize(() => this.saving.set(false))).subscribe({ next: value => { this.upsert(value); this.choose(value); }, error: e => this.error.set(apiErrorMessage(e, 'No se ha podido guardar la localización.')) }); }
  remove(): void { const item = this.selected(); if (!item || !confirm(`¿Eliminar la localización «${item.name}»?`)) return; this.client.delete(this.moduleId, item).subscribe({ next: () => { this.locations.update(items => items.filter(value => value.id !== item.id)); this.newLocation(); }, error: e => this.error.set(apiErrorMessage(e, 'No se ha podido eliminar la localización.')) }); }
  setDetailMap(mapId: string): void { const item = this.selected(); if (!item) return; this.client.setDetailMap(this.moduleId, item, mapId || null).subscribe({ next: value => { this.upsert(value); this.choose(value); }, error: e => this.error.set(apiErrorMessage(e, 'No se ha podido actualizar el mapa detallado.')) }); }
  linkedChapter(item: AdventureLocation, chapterId: string): boolean { return item.chapters.some(chapter => chapter.id === chapterId); }
  toggleChapter(chapter: AdventureChapter, add: boolean): void { const item = this.selected(); if (!item) return; this.client.setChapter(this.moduleId, item, chapter.id, add).subscribe({ next: value => { this.upsert(value); this.selected.set(value); }, error: e => this.error.set(apiErrorMessage(e, 'No se ha podido actualizar el capítulo.')) }); }
  editPoint(point: PointOfInterest): void { this.editingPointId = point.id; this.poiName = point.name; this.poiDescription = point.description ?? ''; this.poiX = point.x; this.poiY = point.y; }
  resetPoint(): void { this.editingPointId = null; this.poiName = ''; this.poiDescription = ''; this.poiX = null; this.poiY = null; }
  savePoint(): void { const item = this.selected(); if (!item) return; const input = { name: this.poiName, description: this.poiDescription, x: this.poiX === null || this.poiX === undefined || Number.isNaN(Number(this.poiX)) ? null : Number(this.poiX), y: this.poiY === null || this.poiY === undefined || Number.isNaN(Number(this.poiY)) ? null : Number(this.poiY) }; const request = this.editingPointId ? this.client.updatePoint(this.moduleId, item, this.editingPointId, input) : this.client.createPoint(this.moduleId, item, input); request.subscribe({ next: value => { this.upsert(value); this.selected.set(value); this.resetPoint(); }, error: e => this.error.set(apiErrorMessage(e, 'No se ha podido guardar el punto de interés.')) }); }
  deletePoint(point: PointOfInterest): void { const item = this.selected(); if (!item || !confirm(`¿Eliminar «${point.name}»?`)) return; this.client.deletePoint(this.moduleId, item, point.id).subscribe({ next: value => { this.upsert(value); this.selected.set(value); }, error: e => this.error.set(apiErrorMessage(e, 'No se ha podido eliminar el punto.')) }); }
  choosePlacementMap(mapId: string): void { this.placementMapId = mapId; this.activeMapId.set(mapId || null); const item = this.selected(); const placement = item?.placements.find(value => value.mapId === mapId); this.placementX = placement?.x ?? 0; this.placementY = placement?.y ?? 0; const map = this.maps().find(value => value.id === mapId); if (map?.hasImage) this.loadImage(map.id); else this.activeMapImage.set(null); }
  savePlacement(): void { const item = this.selected(); if (!item || !this.placementMapId) return; this.client.setPlacement(this.moduleId, item, this.placementMapId, Number(this.placementX), Number(this.placementY)).subscribe({ next: value => { this.upsert(value); this.selected.set(value); }, error: e => this.error.set(apiErrorMessage(e, 'No se ha podido colocar la localización.')) }); }
  removePlacement(mapId: string): void { const item = this.selected(); if (!item) return; this.client.removePlacement(this.moduleId, item, mapId).subscribe({ next: value => { this.upsert(value); this.selected.set(value); }, error: e => this.error.set(apiErrorMessage(e, 'No se ha podido retirar la localización del mapa.')) }); }
  pickPlacement(event: MouseEvent): void { const target = event.currentTarget as HTMLElement; const bounds = target.getBoundingClientRect(); this.placementX = Math.max(0, Math.min(1, (event.clientX - bounds.left) / bounds.width)); this.placementY = Math.max(0, Math.min(1, (event.clientY - bounds.top) / bounds.height)); }
  pickPoint(event: MouseEvent): void { const target = event.currentTarget as HTMLElement; const bounds = target.getBoundingClientRect(); this.poiX = Math.max(0, Math.min(1, (event.clientX - bounds.left) / bounds.width)); this.poiY = Math.max(0, Math.min(1, (event.clientY - bounds.top) / bounds.height)); }
  activeMapName(): string { return this.maps().find(map => map.id === this.activeMapId())?.name ?? 'Mapa'; }
  mapName(mapId: string): string { return this.maps().find(map => map.id === mapId)?.name ?? mapId; }
  private loadImage(mapId: string): void { this.activeMapId.set(mapId); this.mapsClient.imageAdmin(this.moduleId, mapId).subscribe({ next: blob => this.activeMapImage.set(URL.createObjectURL(blob)), error: () => this.error.set('No se ha podido cargar la imagen privada.') }); }
  private upsert(item: AdventureLocation): void { this.locations.update(items => [...items.filter(value => value.id !== item.id), item].sort((a, b) => a.name.localeCompare(b.name))); }
}
