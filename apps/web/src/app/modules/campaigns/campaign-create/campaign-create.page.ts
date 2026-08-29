import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { AdventureModulesClient } from '@modules/adventure-catalog';

import { apiErrorMessage } from '@shared/http/problem-details';

import { CampaignsClient } from '../api/campaigns.client';

@Component({
  selector: 'dnd-campaign-create-page',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './campaign-create.page.html',
  styleUrl: '../campaigns.pages.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampaignCreatePage implements OnInit {
  private readonly campaignsClient = inject(CampaignsClient);
  private readonly modulesClient = inject(AdventureModulesClient);
  private readonly router = inject(Router);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly modules = signal<{ id: string; name: string }[]>([]);
  readonly modulesLoading = signal(true);
  readonly modulesError = signal<string | null>(null);
  readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(3), Validators.maxLength(100)],
    }),
    adventureModuleId: new FormControl('', { nonNullable: true }),
  });

  ngOnInit(): void {
    this.loadModules();
  }

  loadModules(): void {
    this.modulesLoading.set(true);
    this.modulesError.set(null);
    this.modulesClient.options()
      .pipe(finalize(() => this.modulesLoading.set(false)))
      .subscribe({
        next: (modules) => this.modules.set(modules),
        error: (error) => this.modulesError.set(apiErrorMessage(error, 'No se han podido cargar los módulos.')),
      });
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    this.campaignsClient.create(
      this.form.controls.name.value,
      this.form.controls.adventureModuleId.value || null,
    )
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (campaign) => void this.router.navigate(['/campaigns', campaign.id]),
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido crear la campaña.')),
      });
  }
}
