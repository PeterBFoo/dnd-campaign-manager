import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '@shared/http/problem-details';
import { AdventureLocation } from './api/adventure-locations.contracts';
import { AdventureLocationsClient } from './api/adventure-locations.client';
import { AdventureMapsClient } from './api/adventure-maps.client';

@Component({ selector: 'dnd-campaign-adventure-locations', imports: [DecimalPipe, RouterLink], templateUrl: './campaign-adventure-locations.page.html', styleUrl: './campaign-adventure-locations.page.scss', changeDetection: ChangeDetectionStrategy.OnPush })
export class CampaignAdventureLocationsPage implements OnInit {
  private readonly client = inject(AdventureLocationsClient);
  private readonly mapsClient = inject(AdventureMapsClient);
  readonly campaignId = inject(ActivatedRoute).snapshot.paramMap.get('campaignId') ?? '';
  readonly locations = signal<AdventureLocation[]>([]); readonly selected = signal<AdventureLocation | null>(null); readonly loading = signal(true); readonly error = signal<string | null>(null);
  readonly imageSource = signal<string | null>(null);
  ngOnInit(): void { this.client.listCampaign(this.campaignId).pipe(finalize(() => this.loading.set(false))).subscribe({ next: value => this.locations.set(value), error: e => this.error.set(apiErrorMessage(e, 'No tienes acceso a las localizaciones de esta campaña.')) }); }
  open(item: AdventureLocation): void { this.selected.set(item); this.imageSource.set(null); if (item.detailMap?.hasImage) this.mapsClient.imageCampaign(this.campaignId, item.detailMap.id).subscribe({ next: blob => this.imageSource.set(URL.createObjectURL(blob)), error: () => this.error.set('No se ha podido cargar la imagen privada.') }); }
}
