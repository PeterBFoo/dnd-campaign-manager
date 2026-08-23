import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { apiErrorMessage } from '@shared/http/problem-details';

import { CampaignCharacter } from '../api/character.contracts';
import { CharactersClient } from '../api/characters.client';

@Component({
  selector: 'dnd-active-characters-panel',
  imports: [RouterLink],
  templateUrl: './active-characters-panel.component.html',
  styleUrl: '../characters.pages.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ActiveCharactersPanelComponent implements OnInit, OnDestroy {
  private readonly client = inject(CharactersClient);
  readonly campaignId = input.required<string>();
  readonly role = input.required<'dm' | 'player'>();
  readonly characters = signal<CampaignCharacter[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly imageUrls = signal<Record<string, string>>({});

  ngOnInit(): void {
    this.client.list(this.campaignId())
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (characters) => {
          const activeCharacters = characters.filter((character) => character.isActive);
          this.characters.set(activeCharacters);
          this.loadImages(activeCharacters);
        },
        error: (error) => this.error.set(apiErrorMessage(
          error,
          'No se han podido cargar los personajes activos.',
        )),
      });
  }

  ngOnDestroy(): void {
    Object.values(this.imageUrls()).forEach((url) => {
      if (url.startsWith('blob:')) URL.revokeObjectURL(url);
    });
  }

  imageSource(character: CampaignCharacter): string {
    return this.imageUrls()[character.id] ?? 'images/default-character.svg';
  }

  private loadImages(characters: CampaignCharacter[]): void {
    for (const character of characters) {
      if (!character.imageUrl.startsWith('/api/')) continue;
      this.client.image(character.imageUrl).subscribe({
        next: (blob) => this.imageUrls.update((urls) => ({
          ...urls,
          [character.id]: URL.createObjectURL(blob),
        })),
      });
    }
  }
}
