import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { ActiveCharactersPanelComponent } from '@modules/characters';
import { apiErrorMessage } from '@shared/http/problem-details';

import { CampaignSummary } from '../api/campaign.contracts';
import { CampaignsClient } from '../api/campaigns.client';

@Component({
  selector: 'dnd-campaign-detail-page',
  imports: [RouterLink, ActiveCharactersPanelComponent],
  templateUrl: './campaign-detail.page.html',
  styleUrl: '../campaigns.pages.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampaignDetailPage implements OnInit {
  private readonly campaignsClient = inject(CampaignsClient);
  private readonly router = inject(Router);
  private readonly campaignId = inject(ActivatedRoute).snapshot.paramMap.get('campaignId') ?? '';
  readonly campaign = signal<CampaignSummary | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly deleting = signal(false);

  ngOnInit(): void {
    this.campaignsClient.get(this.campaignId)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (campaign) => this.campaign.set(campaign),
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido abrir la campaña.')),
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
