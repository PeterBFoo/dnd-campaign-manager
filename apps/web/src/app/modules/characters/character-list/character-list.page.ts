import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';

import { SessionStore } from '@modules/access';
import { CampaignSummary, CampaignsClient } from '@modules/campaigns';
import { apiErrorMessage } from '@shared/http/problem-details';

import { CampaignCharacter } from '../api/character.contracts';
import { CharactersClient } from '../api/characters.client';

@Component({
  selector: 'dnd-character-list-page',
  imports: [RouterLink],
  templateUrl: './character-list.page.html',
  styleUrl: '../characters.pages.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CharacterListPage implements OnInit, OnDestroy {
  private readonly client = inject(CharactersClient);
  private readonly campaigns = inject(CampaignsClient);
  private readonly session = inject(SessionStore);
  readonly campaignId = inject(ActivatedRoute).snapshot.paramMap.get('campaignId') ?? '';
  readonly campaign = signal<CampaignSummary | null>(null);
  readonly characters = signal<CampaignCharacter[]>([]);
  readonly loading = signal(true);
  readonly changing = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly imageUrls = signal<Record<string, string>>({});
  readonly currentUserId = computed(() => this.session.user()?.id ?? '');
  readonly visibleCharacters = computed(() => this.campaign()?.role === 'dm'
    ? this.characters()
    : this.characters().filter((character) => character.ownerUserId === this.currentUserId()));

  ngOnInit(): void {
    forkJoin({ campaign: this.campaigns.get(this.campaignId), characters: this.client.list(this.campaignId) })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ campaign, characters }) => {
          this.campaign.set(campaign);
          this.characters.set(characters);
          this.loadImages(this.charactersForManagement(campaign, characters));
        },
        error: (error) => this.error.set(apiErrorMessage(error, 'No se han podido cargar los personajes.')),
      });
  }

  ngOnDestroy(): void {
    Object.values(this.imageUrls()).forEach((url) => {
      if (url.startsWith('blob:')) URL.revokeObjectURL(url);
    });
  }

  canManage(character: CampaignCharacter): boolean {
    return this.campaign()?.role === 'dm' || character.ownerUserId === this.currentUserId();
  }

  canActivate(character: CampaignCharacter): boolean {
    return this.campaign()?.role === 'player'
      && character.ownerUserId === this.currentUserId()
      && !character.isActive;
  }

  activate(character: CampaignCharacter): void {
    if (!this.canActivate(character) || this.changing()) return;
    this.changing.set(character.id);
    this.error.set(null);
    this.client.activate(this.campaignId, character.id)
      .pipe(finalize(() => this.changing.set(null)))
      .subscribe({
        next: () => this.characters.update((items) => items.map((item) => ({
          ...item,
          isActive: item.ownerUserId === character.ownerUserId ? item.id === character.id : item.isActive,
        }))),
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido activar el personaje.')),
      });
  }

  remove(character: CampaignCharacter): void {
    if (!this.canManage(character) || this.changing()) return;
    if (!window.confirm(`¿Eliminar a ${character.name}? Esta acción no se puede deshacer.`)) return;
    this.changing.set(character.id);
    this.error.set(null);
    this.client.delete(this.campaignId, character.id)
      .pipe(finalize(() => this.changing.set(null)))
      .subscribe({
        next: () => this.reloadCharacters(),
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido eliminar el personaje.')),
      });
  }

  imageSource(character: CampaignCharacter): string {
    return this.imageUrls()[character.id] ?? 'images/default-character.svg';
  }

  private reloadCharacters(): void {
    this.client.list(this.campaignId).subscribe({
      next: (characters) => {
        this.characters.set(characters);
        this.loadImages(this.charactersForManagement(this.campaign(), characters));
      },
      error: (error) => this.error.set(apiErrorMessage(error, 'No se han podido recargar los personajes.')),
    });
  }

  private loadImages(characters: CampaignCharacter[]): void {
    for (const character of characters) {
      if (!character.imageUrl.startsWith('/api/')) continue;
      this.client.image(character.imageUrl).subscribe({
        next: (blob) => {
          const previous = this.imageUrls()[character.id];
          if (previous?.startsWith('blob:')) URL.revokeObjectURL(previous);
          this.imageUrls.update((urls) => ({ ...urls, [character.id]: URL.createObjectURL(blob) }));
        },
      });
    }
  }

  private charactersForManagement(
    campaign: CampaignSummary | null,
    characters: CampaignCharacter[],
  ): CampaignCharacter[] {
    return campaign?.role === 'dm'
      ? characters
      : characters.filter((character) => character.ownerUserId === this.currentUserId());
  }

}
