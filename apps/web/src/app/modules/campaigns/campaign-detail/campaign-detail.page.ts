import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { finalize } from 'rxjs';

import { AdventureModuleOption, AdventureModulesClient } from '@modules/adventure-catalog';
import { ActiveCharactersPanelComponent } from '@modules/characters';
import { apiErrorMessage } from '@shared/http/problem-details';

import { CampaignSummary } from '../api/campaign.contracts';
import { CampaignsClient } from '../api/campaigns.client';

@Component({
  selector: 'dnd-campaign-detail-page',
  imports: [RouterLink, ReactiveFormsModule, ActiveCharactersPanelComponent],
  templateUrl: './campaign-detail.page.html',
  styleUrl: '../campaigns.pages.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampaignDetailPage implements OnInit {
  private readonly campaignsClient = inject(CampaignsClient);
  private readonly modulesClient = inject(AdventureModulesClient);
  private readonly router = inject(Router);
  private readonly campaignId = inject(ActivatedRoute).snapshot.paramMap.get('campaignId') ?? '';
  readonly campaign = signal<CampaignSummary | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly deleting = signal(false);
  readonly modules = signal<AdventureModuleOption[]>([]);
  readonly modulesLoading = signal(false);
  readonly modulesError = signal<string | null>(null);
  readonly updatingModule = signal(false);
  readonly moduleSelection = new FormControl('', { nonNullable: true });
  readonly moduleCoverUrl = signal<string | null>(null);

  ngOnInit(): void {
    this.campaignsClient.get(this.campaignId)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (campaign) => {
          this.campaign.set(campaign);
          this.moduleSelection.setValue(campaign.adventureModuleId ?? '');
          this.loadModuleCover(campaign.adventureModule?.id ?? null);
          if (campaign.role === 'dm') this.loadModules();
        },
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido abrir la campaña.')),
      });
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

  assignModule(): void {
    const current = this.campaign();
    const moduleId = this.moduleSelection.value;
    if (!current || current.role !== 'dm' || !moduleId || this.updatingModule()) return;
    if (!window.confirm(current.adventureModuleId
      ? '¿Cambiar el módulo de esta campaña? El módulo anterior no se eliminará.'
      : '¿Asignar este módulo a la campaña?')) return;

    this.updateModule(() => this.campaignsClient.assignModule(current.id, moduleId, current.version));
  }

  removeModule(): void {
    const current = this.campaign();
    if (!current || current.role !== 'dm' || !current.adventureModuleId || this.updatingModule()) return;
    if (!window.confirm('¿Retirar el módulo de esta campaña? El módulo no se eliminará del catálogo.')) return;
    this.updateModule(() => this.campaignsClient.removeModule(current.id, current.version));
  }

  private updateModule(request: () => ReturnType<CampaignsClient['assignModule']>): void {
    this.modulesError.set(null);
    this.updatingModule.set(true);
    request().pipe(finalize(() => this.updatingModule.set(false))).subscribe({
      next: (campaign) => {
        this.campaign.set(campaign);
        this.moduleSelection.setValue(campaign.adventureModuleId ?? '');
        this.loadModuleCover(campaign.adventureModule?.id ?? null);
      },
      error: (error) => this.modulesError.set(apiErrorMessage(error, 'No se ha podido actualizar el módulo.')),
    });
  }

  private loadModuleCover(moduleId: string | null): void {
    this.moduleCoverUrl.set(null);
    if (!moduleId) return;
    this.modulesClient.campaignCover(moduleId).subscribe({
      next: (cover) => this.moduleCoverUrl.set(URL.createObjectURL(cover)),
    });
  }

  deleteCampaign(campaign: CampaignSummary): void {
    if (campaign.role !== 'dm'
      || !window.confirm(`¿Eliminar la campaña «${campaign.name}»? Perderás el acceso y esta acción no se puede deshacer.`)) {
      return;
    }

    this.error.set(null);
    this.deleting.set(true);
    this.campaignsClient.delete(campaign.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => void this.router.navigate(['/campaigns'], { replaceUrl: true }),
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido eliminar la campaña.')),
      });
  }
}
