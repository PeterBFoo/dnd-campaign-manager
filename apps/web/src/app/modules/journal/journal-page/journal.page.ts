import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';

import { SessionStore } from '@modules/access';
import { CampaignSummary, CampaignsClient } from '@modules/campaigns';
import { CampaignCharacter, CharactersClient } from '@modules/characters';
import { apiErrorMessage } from '@shared/http/problem-details';

import { JournalClient } from '../api/journal.client';
import { JournalEntry } from '../api/journal.contracts';

@Component({
  selector: 'dnd-journal-page',
  imports: [DatePipe, ReactiveFormsModule, RouterLink],
  templateUrl: './journal.page.html',
  styleUrl: './journal.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JournalPage implements OnInit {
  private readonly client = inject(JournalClient);
  private readonly campaigns = inject(CampaignsClient);
  private readonly charactersClient = inject(CharactersClient);
  private readonly session = inject(SessionStore);
  readonly campaignId = inject(ActivatedRoute).snapshot.paramMap.get('campaignId') ?? '';
  readonly campaign = signal<CampaignSummary | null>(null);
  readonly characters = signal<CampaignCharacter[]>([]);
  readonly entries = signal<JournalEntry[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly loading = signal(true);
  readonly loadingMore = signal(false);
  readonly saving = signal(false);
  readonly deletingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly content = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(5_000)],
  });
  readonly form = new FormGroup({ content: this.content });
  readonly currentUserId = computed(() => this.session.user()?.id ?? '');
  readonly isPlayer = computed(() => this.campaign()?.role === 'player');
  readonly hasActiveCharacter = computed(() => this.characters().some((character) =>
    character.ownerUserId === this.currentUserId() && character.isActive));
  readonly canShowForm = computed(() => this.isPlayer()
    && (this.hasActiveCharacter() || this.editingId() !== null));

  ngOnInit(): void {
    forkJoin({
      campaign: this.campaigns.get(this.campaignId),
      characters: this.charactersClient.list(this.campaignId),
      page: this.client.list(this.campaignId),
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: ({ campaign, characters, page }) => {
        this.campaign.set(campaign);
        this.characters.set(characters);
        this.entries.set(page.items);
        this.nextCursor.set(page.nextCursor);
      },
      error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido cargar la bitácora.')),
    });
  }

  submit(): void {
    if (!this.canShowForm() || this.content.invalid || this.saving()) {
      this.content.markAsTouched();
      return;
    }

    const editingId = this.editingId();
    const request = editingId
      ? this.client.update(this.campaignId, editingId, this.content.value)
      : this.client.create(this.campaignId, this.content.value);
    this.saving.set(true);
    this.error.set(null);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (entry) => {
        if (editingId) {
          this.entries.update((items) => items.map((item) => item.id === entry.id ? entry : item));
        } else {
          this.entries.update((items) => [entry, ...items.filter((item) => item.id !== entry.id)]);
        }
        this.cancelEdit();
      },
      error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido guardar la entrada.')),
    });
  }

  beginEdit(entry: JournalEntry): void {
    if (!entry.canEdit || this.saving()) return;
    this.editingId.set(entry.id);
    this.content.setValue(entry.content);
    this.content.markAsUntouched();
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.content.reset('');
  }

  remove(entry: JournalEntry): void {
    if (!entry.canDelete || this.deletingId()) return;
    if (!window.confirm('¿Eliminar esta entrada? Esta acción no se puede deshacer.')) return;
    this.deletingId.set(entry.id);
    this.error.set(null);
    this.client.delete(this.campaignId, entry.id)
      .pipe(finalize(() => this.deletingId.set(null)))
      .subscribe({
        next: () => {
          this.entries.update((items) => items.filter((item) => item.id !== entry.id));
          if (this.editingId() === entry.id) this.cancelEdit();
        },
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido eliminar la entrada.')),
      });
  }

  loadMore(): void {
    const cursor = this.nextCursor();
    if (!cursor || this.loadingMore()) return;
    this.loadingMore.set(true);
    this.error.set(null);
    this.client.list(this.campaignId, cursor)
      .pipe(finalize(() => this.loadingMore.set(false)))
      .subscribe({
        next: (page) => {
          this.entries.update((items) => {
            const known = new Set(items.map((item) => item.id));
            return [...items, ...page.items.filter((item) => !known.has(item.id))];
          });
          this.nextCursor.set(page.nextCursor);
        },
        error: (error) => this.error.set(apiErrorMessage(error, 'No se han podido cargar entradas anteriores.')),
      });
  }
}
