import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Observable, finalize, forkJoin } from 'rxjs';

import { SessionStore } from '@modules/access';
import { CampaignSummary, CampaignsClient } from '@modules/campaigns';
import { CampaignCharacter, CharactersClient } from '@modules/characters';
import { apiErrorMessage } from '@shared/http/problem-details';

import { Mission, MissionStatus } from '../api/mission.contracts';
import { MissionsClient } from '../api/missions.client';

@Component({
  selector: 'dnd-mission-page',
  imports: [DatePipe, NgTemplateOutlet, ReactiveFormsModule, RouterLink],
  templateUrl: './mission.page.html',
  styleUrl: './mission.page.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MissionPage implements OnInit {
  private readonly client = inject(MissionsClient);
  private readonly campaigns = inject(CampaignsClient);
  private readonly charactersClient = inject(CharactersClient);
  private readonly session = inject(SessionStore);
  readonly campaignId = inject(ActivatedRoute).snapshot.paramMap.get('campaignId') ?? '';
  readonly campaign = signal<CampaignSummary | null>(null);
  readonly characters = signal<CampaignCharacter[]>([]);
  readonly missions = signal<Mission[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly operatingId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly title = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(2), Validators.maxLength(120)],
  });
  readonly description = new FormControl('', {
    nonNullable: true,
    validators: [Validators.maxLength(5_000)],
  });
  readonly status = new FormControl<MissionStatus>('active', { nonNullable: true });
  readonly isMain = new FormControl(false, { nonNullable: true });
  readonly form = new FormGroup({
    title: this.title,
    description: this.description,
    status: this.status,
    isMain: this.isMain,
  });
  readonly currentUserId = computed(() => this.session.user()?.id ?? '');
  readonly isPlayer = computed(() => this.campaign()?.role === 'player');
  readonly hasActiveCharacter = computed(() => this.characters().some((character) =>
    character.ownerUserId === this.currentUserId() && character.isActive));
  readonly canCreate = computed(() => this.campaign()?.role === 'dm' || this.hasActiveCharacter());
  readonly canShowForm = computed(() => this.editingId() !== null || this.canCreate());
  readonly mainMission = computed(() => this.missions().find((mission) => mission.isMain) ?? null);
  readonly activeMissions = computed(() => this.missions().filter((mission) =>
    mission.status === 'active' && !mission.isMain));
  readonly closedMissions = computed(() => this.missions().filter((mission) => mission.status !== 'active'));

  ngOnInit(): void {
    forkJoin({
      campaign: this.campaigns.get(this.campaignId),
      characters: this.charactersClient.list(this.campaignId),
      missions: this.client.list(this.campaignId),
    }).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: ({ campaign, characters, missions }) => {
        this.campaign.set(campaign);
        this.characters.set(characters);
        this.missions.set(missions.items);
      },
      error: (error) => this.error.set(apiErrorMessage(error, 'No se han podido cargar las misiones.')),
    });
  }

  submit(): void {
    if (!this.canShowForm() || this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }
    const editingId = this.editingId();
    const description = this.description.value.trim() || null;
    const request = editingId
      ? this.client.update(this.campaignId, editingId, {
        title: this.title.value,
        description,
        status: this.status.value,
      })
      : this.client.create(this.campaignId, {
        title: this.title.value,
        description,
        isMain: this.isMain.value,
      });
    this.saving.set(true);
    this.error.set(null);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.cancelEdit();
        this.reload('No se ha podido actualizar el registro de misiones.');
      },
      error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido guardar la misión.')),
    });
  }

  beginEdit(mission: Mission): void {
    if (this.saving()) return;
    this.editingId.set(mission.id);
    this.title.setValue(mission.title);
    this.description.setValue(mission.description ?? '');
    this.status.setValue(mission.status);
    this.isMain.setValue(false);
    this.form.markAsUntouched();
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.form.reset({ title: '', description: '', status: 'active', isMain: false });
  }

  setMain(mission: Mission): void {
    this.operate(mission, this.client.setMain(this.campaignId, mission.id),
      'No se ha podido marcar la misión principal.');
  }

  clearMain(mission: Mission): void {
    this.operate(mission, this.client.clearMain(this.campaignId, mission.id),
      'No se ha podido desmarcar la misión principal.');
  }

  remove(mission: Mission): void {
    if (!mission.canDelete || this.operatingId()) return;
    if (!window.confirm('¿Eliminar esta misión? Esta acción no se puede deshacer.')) return;
    this.operate(mission, this.client.delete(this.campaignId, mission.id),
      'No se ha podido eliminar la misión.', true);
  }

  statusLabel(status: MissionStatus): string {
    return ({
      active: 'Activa',
      completed: 'Completada',
      failed: 'Fallida',
      cancelled: 'Cancelada',
    })[status];
  }

  private operate(
    mission: Mission,
    request: Observable<unknown>,
    fallback: string,
    remove = false,
  ): void {
    if (this.operatingId()) return;
    this.operatingId.set(mission.id);
    this.error.set(null);
    request.pipe(finalize(() => this.operatingId.set(null))).subscribe({
      next: () => {
        if (remove) {
          this.missions.update((items) => items.filter((item) => item.id !== mission.id));
          if (this.editingId() === mission.id) this.cancelEdit();
        } else {
          this.reload(fallback);
        }
      },
      error: (error) => {
        this.error.set(apiErrorMessage(error, fallback));
        this.reload(fallback, false);
      },
    });
  }

  private reload(fallback: string, clearError = true): void {
    if (clearError) this.error.set(null);
    this.client.list(this.campaignId).subscribe({
      next: (response) => this.missions.set(response.items),
      error: (error) => this.error.set(apiErrorMessage(error, fallback)),
    });
  }
}
