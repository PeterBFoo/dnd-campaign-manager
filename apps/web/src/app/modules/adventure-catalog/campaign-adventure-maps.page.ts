import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AdventureMapsClient } from './api/adventure-maps.client';
import { AdventureMap } from './api/adventure-maps.contracts';

@Component({ selector: 'dnd-campaign-adventure-maps', imports: [RouterLink], templateUrl: './campaign-adventure-maps.page.html', changeDetection: ChangeDetectionStrategy.OnPush })
export class CampaignAdventureMapsPage implements OnInit {
  private readonly client = inject(AdventureMapsClient); readonly campaignId = inject(ActivatedRoute).snapshot.paramMap.get('campaignId') ?? '';
  readonly maps = signal<AdventureMap[]>([]); readonly selected = signal<AdventureMap | null>(null); readonly imageSource = signal<string | null>(null); readonly loading = signal(true); readonly error = signal<string | null>(null);
  ngOnInit(): void { this.client.listCampaign(this.campaignId).pipe(finalize(() => this.loading.set(false))).subscribe({ next: maps => this.maps.set(maps), error: () => this.error.set('No tienes acceso a los mapas de esta campaña.') }); }
  open(map: AdventureMap): void { this.selected.set(map); this.imageSource.set(null); if (map.hasImage) this.client.imageCampaign(this.campaignId, map.id).subscribe({ next: blob => this.imageSource.set(URL.createObjectURL(blob)), error: () => this.error.set('No se ha podido cargar la imagen privada.') }); }
}
