import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '@shared/http/problem-details';
import { AdventureMapsClient } from './api/adventure-maps.client';
import { AdventureMap, AdventureMapChapter } from './api/adventure-maps.contracts';

@Component({ selector: 'dnd-adventure-maps-admin', imports: [FormsModule, RouterLink], templateUrl: './adventure-maps-admin.page.html', changeDetection: ChangeDetectionStrategy.OnPush })
export class AdventureMapsAdminPage implements OnInit {
  private readonly client = inject(AdventureMapsClient); readonly moduleId = inject(ActivatedRoute).snapshot.paramMap.get('moduleId') ?? '';
  readonly maps = signal<AdventureMap[]>([]); readonly chapters = signal<AdventureMapChapter[]>([]); readonly selected = signal<AdventureMap | null>(null);
  readonly loading = signal(true); readonly saving = signal(false); readonly error = signal<string | null>(null); readonly imageSource = signal<string | null>(null);
  name = ''; description = ''; image: File | null = null; originKind = 'Original'; sourceReference = ''; rightsBasis = ''; attribution = '';
  ngOnInit(): void { this.reload(); this.client.chapters(this.moduleId).subscribe({ next: value => this.chapters.set(value) }); }
  reload(): void { this.loading.set(true); this.client.listAdmin(this.moduleId).pipe(finalize(() => this.loading.set(false))).subscribe({ next: value => this.maps.set(value), error: e => this.error.set(apiErrorMessage(e, 'No se han podido cargar los mapas.')) }); }
  choose(map: AdventureMap): void { this.selected.set(map); this.name = map.name; this.description = map.description ?? ''; this.imageSource.set(null); if (map.hasImage) this.client.imageAdmin(this.moduleId, map.id).subscribe({ next: blob => this.imageSource.set(URL.createObjectURL(blob)), error: () => this.error.set('No se ha podido cargar la imagen privada.') }); }
  newMap(): void { this.selected.set(null); this.name = ''; this.description = ''; this.imageSource.set(null); }
  save(): void { this.saving.set(true); this.error.set(null); const current = this.selected(); const request = current ? this.client.update(this.moduleId, current, this.name, this.description) : this.client.create(this.moduleId, this.name, this.description); request.pipe(finalize(() => this.saving.set(false))).subscribe({ next: map => { this.upsert(map); this.choose(map); }, error: e => this.error.set(apiErrorMessage(e, 'No se ha podido guardar el mapa.')) }); }
  selectImage(event: Event): void { this.image = (event.target as HTMLInputElement).files?.item(0) ?? null; }
  upload(): void { const map = this.selected(); if (!map || !this.image || !this.rightsBasis.trim()) return; this.saving.set(true); this.client.putImage(this.moduleId, map, this.image, { originKind: this.originKind, sourceReference: this.sourceReference, rightsBasis: this.rightsBasis, attribution: this.attribution }).pipe(finalize(() => this.saving.set(false))).subscribe({ next: value => { this.upsert(value); this.choose(value); this.image = null; }, error: e => this.error.set(apiErrorMessage(e, 'No se ha podido cargar la imagen.')) }); }
  removeImage(): void { const map = this.selected(); if (!map || !confirm('¿Retirar la imagen del mapa?')) return; this.client.removeImage(this.moduleId, map).subscribe({ next: value => { this.upsert(value); this.choose(value); } }); }
  toggleChapter(chapter: AdventureMapChapter, add: boolean): void { const map = this.selected(); if (!map) return; this.client.setChapter(this.moduleId, map, chapter.id, add).subscribe({ next: value => { this.upsert(value); this.selected.set(value); }, error: e => this.error.set(apiErrorMessage(e, 'No se ha podido actualizar el capítulo.')) }); }
  linked(map: AdventureMap, chapter: AdventureMapChapter): boolean { return map.chapters.some(item => item.id === chapter.id); }
  remove(): void { const map = this.selected(); if (!map || !confirm(`¿Eliminar el mapa «${map.name}»?`)) return; this.client.delete(this.moduleId, map).subscribe({ next: () => { this.maps.update(items => items.filter(item => item.id !== map.id)); this.newMap(); } }); }
  private upsert(map: AdventureMap): void { this.maps.update(items => [...items.filter(item => item.id !== map.id), map].sort((a, b) => a.name.localeCompare(b.name))); }
}
