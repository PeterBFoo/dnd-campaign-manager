import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EMPTY, Observable, catchError, exhaustMap, finalize, throwError, timer } from 'rxjs';

import { CampaignSummary, CampaignsClient } from '@modules/campaigns';
import { CampaignCharacter, CharactersClient } from '@modules/characters';
import { apiErrorMessage } from '@shared/http/problem-details';

import { CombatClient } from '../api/combat.client';
import {
  ActiveEncounter,
  DmEncounter,
  DmEnemyMember,
  DmParticipant,
  EncounterSummary,
} from '../api/combat.contracts';

@Component({
  selector: 'dnd-encounter-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './encounter.page.html',
  styleUrl: '../combat.pages.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EncounterPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly campaignsClient = inject(CampaignsClient);
  private readonly charactersClient = inject(CharactersClient);
  private readonly combatClient = inject(CombatClient);

  readonly campaignId = this.route.snapshot.paramMap.get('campaignId') ?? '';
  readonly encounterId = this.route.snapshot.paramMap.get('encounterId');
  readonly campaign = signal<CampaignSummary | null>(null);
  readonly encounters = signal<EncounterSummary[]>([]);
  readonly encounter = signal<DmEncounter | null>(null);
  readonly activeEncounter = signal<ActiveEncounter | null>(null);
  readonly characters = signal<CampaignCharacter[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  readonly createForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(120)] }),
  });
  readonly renameForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(120)] }),
  });
  readonly characterForm = new FormGroup({
    characterId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    initiative: new FormControl<number | null>(null, [Validators.required, Validators.min(-20), Validators.max(30)]),
  });
  readonly enemyForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(80)] }),
    initiative: new FormControl<number | null>(null, [Validators.required, Validators.min(-20), Validators.max(30)]),
    armorClass: new FormControl<number | null>(null, [Validators.required, Validators.min(0), Validators.max(40)]),
    maximumHitPoints: new FormControl<number | null>(null, [Validators.required, Validators.min(1), Validators.max(100000)]),
    quantity: new FormControl(1, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(100)] }),
  });

  readonly availableCharacters = computed(() => {
    const used = new Set(this.encounter()?.participants
      .map((participant) => participant.characterId)
      .filter((id): id is string => id !== null) ?? []);
    return this.characters().filter((character) => !used.has(character.id));
  });

  ngOnInit(): void {
    this.campaignsClient.get(this.campaignId).subscribe({
      next: (campaign) => {
        this.campaign.set(campaign);
        if (campaign.role === 'dm') {
          this.loadDmExperience();
        } else {
          this.startPlayerPolling();
        }
      },
      error: (error) => {
        this.loading.set(false);
        this.error.set(apiErrorMessage(error, 'No se ha podido abrir la campaña.'));
      },
    });
  }

  createEncounter(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    this.combatClient.create(this.campaignId, this.createForm.controls.name.value)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: (encounter) => void this.router.navigate([
          '/campaigns', this.campaignId, 'encounters', encounter.id,
        ]),
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido crear el encuentro.')),
      });
  }

  renameEncounter(): void {
    const encounter = this.encounter();
    if (!encounter || this.renameForm.invalid) {
      this.renameForm.markAllAsTouched();
      return;
    }
    this.apply(this.combatClient.rename(
      this.campaignId, encounter.id, this.renameForm.controls.name.value, encounter.version,
    ));
  }

  addCharacter(): void {
    const encounter = this.encounter();
    if (!encounter || this.characterForm.invalid) {
      this.characterForm.markAllAsTouched();
      return;
    }
    const value = this.characterForm.getRawValue();
    this.apply(this.combatClient.addCharacter(
      this.campaignId, encounter.id, value.characterId, value.initiative!, encounter.version,
    ), () => this.characterForm.reset({ characterId: '', initiative: null }));
  }

  addEnemy(): void {
    const encounter = this.encounter();
    if (!encounter || this.enemyForm.invalid) {
      this.enemyForm.markAllAsTouched();
      return;
    }
    const value = this.enemyForm.getRawValue();
    this.apply(this.combatClient.addEnemy(this.campaignId, encounter.id, {
      name: value.name,
      initiative: value.initiative!,
      armorClass: value.armorClass!,
      maximumHitPoints: value.maximumHitPoints!,
      quantity: value.quantity,
      expectedVersion: encounter.version,
    }), () => this.enemyForm.reset({ name: '', initiative: null, armorClass: null, maximumHitPoints: null, quantity: 1 }));
  }

  changeInitiative(participant: DmParticipant, rawValue: string): void {
    const encounter = this.encounter();
    const initiative = Number(rawValue);
    if (!encounter || !Number.isInteger(initiative) || initiative < -20 || initiative > 30) {
      this.error.set('La iniciativa debe ser un entero entre -20 y 30.');
      return;
    }
    this.apply(this.combatClient.changeInitiative(
      this.campaignId, encounter.id, participant.id, initiative, encounter.version,
    ));
  }

  removeParticipant(participant: DmParticipant): void {
    const encounter = this.encounter();
    if (!encounter) return;
    this.apply(this.combatClient.removeParticipant(
      this.campaignId, encounter.id, participant.id, encounter.version,
    ));
  }

  moveParticipant(index: number, direction: -1 | 1): void {
    const encounter = this.encounter();
    if (!encounter) return;
    const target = index + direction;
    if (target < 0 || target >= encounter.participants.length) return;
    if (encounter.participants[index].initiative !== encounter.participants[target].initiative) return;
    const participants = [...encounter.participants];
    [participants[index], participants[target]] = [participants[target], participants[index]];
    this.encounter.set({
      ...encounter,
      participants: participants.map((participant, orderPosition) => ({ ...participant, orderPosition })),
    });
  }

  confirmOrder(): void {
    const encounter = this.encounter();
    if (!encounter) return;
    this.apply(this.combatClient.confirmOrder(
      this.campaignId,
      encounter.id,
      encounter.participants.map((participant) => participant.id),
      encounter.version,
    ));
  }

  activate(): void {
    const encounter = this.encounter();
    if (!encounter) return;
    this.apply(this.combatClient.activate(this.campaignId, encounter.id, encounter.version), () => this.loadList());
  }

  advance(): void {
    const encounter = this.encounter();
    if (!encounter) return;
    this.apply(this.combatClient.advance(this.campaignId, encounter.id, encounter.version));
  }

  adjustHitPoints(
    participant: DmParticipant,
    member: DmEnemyMember,
    kind: 'damage' | 'healing',
    rawAmount: string,
  ): void {
    const encounter = this.encounter();
    const amount = Number(rawAmount);
    if (!encounter || !Number.isInteger(amount) || amount < 1 || amount > 100000) {
      this.error.set('La cantidad debe ser un entero positivo.');
      return;
    }
    this.apply(this.combatClient.adjustHitPoints(
      this.campaignId, encounter.id, participant.id, member.id, kind, amount, encounter.version,
    ));
  }

  deleteEncounter(target: Pick<DmEncounter, 'id' | 'name' | 'status' | 'version'>): void {
    if (target.status === 'active') {
      this.error.set('Finaliza el encuentro antes de eliminarlo.');
      return;
    }
    if (!window.confirm(`¿Eliminar el encuentro «${target.name}»? Esta acción no se puede deshacer.`)) return;
    this.saving.set(true);
    this.error.set(null);
    this.combatClient.deleteEncounter(this.campaignId, target.id, target.version)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          if (this.encounterId) {
            void this.router.navigate(['/campaigns', this.campaignId, 'encounters']);
          } else {
            this.loadList();
          }
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(apiErrorMessage(error, 'No se ha podido eliminar el encuentro.'));
          if (error.status === 409 && this.encounterId) this.loadEncounter();
        },
      });
  }

  finish(): void {
    const encounter = this.encounter();
    if (!encounter || !window.confirm('¿Finalizar este encuentro? No podrá reabrirse.')) return;
    this.apply(this.combatClient.finish(this.campaignId, encounter.id, encounter.version), () => this.loadList());
  }

  canMove(index: number, direction: -1 | 1): boolean {
    const participants = this.encounter()?.participants ?? [];
    const target = index + direction;
    return target >= 0
      && target < participants.length
      && participants[index].initiative === participants[target].initiative;
  }

  statusLabel(status: EncounterSummary['status']): string {
    return status === 'draft' ? 'Borrador' : status === 'active' ? 'Activo' : 'Finalizado';
  }

  currentParticipantName(encounter: DmEncounter): string {
    return encounter.participants.find((participant) => participant.isCurrentTurn)?.name ?? 'Participante';
  }

  private loadDmExperience(): void {
    this.loadList();
    this.charactersClient.list(this.campaignId).subscribe({
      next: (characters) => this.characters.set(characters),
      error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido cargar el elenco.')),
    });
    if (this.encounterId) {
      this.loadEncounter();
    } else {
      this.loading.set(false);
    }
  }

  private loadList(): void {
    this.combatClient.list(this.campaignId).subscribe({
      next: (response) => this.encounters.set(response.items),
      error: (error) => this.error.set(apiErrorMessage(error, 'No se han podido cargar los encuentros.')),
    });
  }

  private loadEncounter(): void {
    if (!this.encounterId) return;
    this.loading.set(true);
    this.combatClient.get(this.campaignId, this.encounterId)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (encounter) => this.setEncounter(encounter),
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido cargar el encuentro.')),
      });
  }

  private startPlayerPolling(): void {
    timer(0, 5000).pipe(
      exhaustMap(() => this.combatClient.active(this.campaignId).pipe(
        catchError((error: HttpErrorResponse) => {
          this.loading.set(false);
          this.error.set(apiErrorMessage(error, 'No se ha podido actualizar el encuentro activo.'));
          return error.status === 401 || error.status === 403 ? throwError(() => error) : EMPTY;
        }),
      )),
      takeUntilDestroyed(this.destroyRef),
    ).subscribe({
      next: (response) => {
        this.activeEncounter.set(response.encounter);
        this.loading.set(false);
        this.error.set(null);
      },
      error: () => undefined,
    });
  }

  private apply(operation: Observable<DmEncounter>, afterSuccess?: () => void): void {
    this.saving.set(true);
    this.error.set(null);
    operation.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (encounter) => {
        this.setEncounter(encounter);
        afterSuccess?.();
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(apiErrorMessage(error, 'No se ha podido actualizar el encuentro.'));
        if (error.status === 409) this.loadEncounter();
      },
    });
  }

  private setEncounter(encounter: DmEncounter): void {
    this.encounter.set(encounter);
    this.renameForm.setValue({ name: encounter.name });
  }
}
