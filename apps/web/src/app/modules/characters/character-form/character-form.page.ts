import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize, forkJoin, of } from 'rxjs';

import { CampaignSummary, CampaignsClient } from '@modules/campaigns';
import { apiErrorMessage } from '@shared/http/problem-details';

import { CampaignCharacter, CharacterOwner } from '../api/character.contracts';
import { CharactersClient } from '../api/characters.client';

@Component({
  selector: 'dnd-character-form-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './character-form.page.html',
  styleUrl: '../characters.pages.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CharacterFormPage implements OnInit {
  private readonly client = inject(CharactersClient);
  private readonly campaigns = inject(CampaignsClient);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly campaignId = this.route.snapshot.paramMap.get('campaignId') ?? '';
  readonly characterId = this.route.snapshot.paramMap.get('characterId');
  readonly editing = this.characterId !== null;
  readonly campaign = signal<CampaignSummary | null>(null);
  readonly owners = signal<CharacterOwner[]>([]);
  readonly existing = signal<CampaignCharacter | null>(null);
  readonly selectedImage = signal<File | null>(null);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2), Validators.maxLength(80)] }),
    armorClass: new FormControl<number | null>(null, [Validators.required, Validators.min(0), Validators.max(40)]),
    initiative: new FormControl<number | null>(null, [Validators.required, Validators.min(-20), Validators.max(30)]),
    ownerUserId: new FormControl<string | null>(null),
    removeImage: new FormControl(false, { nonNullable: true }),
  });

  ngOnInit(): void {
    forkJoin({
      campaign: this.campaigns.get(this.campaignId),
      characters: this.editing ? this.client.list(this.campaignId) : of([]),
    }).subscribe({
      next: ({ campaign, characters }) => {
        this.campaign.set(campaign);
        if (campaign.role === 'dm') {
          this.client.owners(this.campaignId).subscribe({
            next: (owners) => this.owners.set(owners),
            error: (error) => this.error.set(apiErrorMessage(error, 'No se han podido cargar los jugadores.')),
          });
        }
        if (this.characterId) {
          const character = characters.find((item) => item.id === this.characterId) ?? null;
          if (!character) {
            this.error.set('No se ha encontrado el personaje.');
          } else {
            this.existing.set(character);
            this.form.patchValue({
              name: character.name,
              armorClass: character.armorClass,
              initiative: character.initiative,
              ownerUserId: character.ownerUserId,
            });
          }
        }
        this.loading.set(false);
      },
      error: (error) => {
        this.loading.set(false);
        this.error.set(apiErrorMessage(error, 'No se ha podido abrir el formulario.'));
      },
    });
  }

  selectImage(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.selectedImage.set(file);
    if (file) this.form.controls.removeImage.setValue(false);
  }

  submit(): void {
    const image = this.selectedImage();
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    if (image && (!['image/jpeg', 'image/png', 'image/webp'].includes(image.type) || image.size > 5 * 1024 * 1024)) {
      this.error.set('La imagen debe ser JPEG, PNG o WebP y no superar 5 MiB.');
      return;
    }

    const raw = this.form.getRawValue();
    const value = {
      name: raw.name,
      armorClass: raw.armorClass!,
      initiative: raw.initiative!,
      ownerUserId: this.campaign()?.role === 'dm' ? raw.ownerUserId : undefined,
      image,
      removeImage: raw.removeImage,
    };
    this.submitting.set(true);
    this.error.set(null);
    const request = this.characterId
      ? this.client.update(this.campaignId, this.characterId, value)
      : this.client.create(this.campaignId, value);
    request.pipe(finalize(() => this.submitting.set(false))).subscribe({
      next: () => void this.router.navigate(['/campaigns', this.campaignId, 'characters']),
      error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido guardar el personaje.')),
    });
  }
}
