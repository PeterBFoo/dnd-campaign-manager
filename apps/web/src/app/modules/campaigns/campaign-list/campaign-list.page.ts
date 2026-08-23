import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { apiErrorMessage } from '@shared/http/problem-details';

import { CampaignSummary } from '../api/campaign.contracts';
import { CampaignsClient } from '../api/campaigns.client';

@Component({
  selector: 'dnd-campaign-list-page',
  imports: [RouterLink],
  templateUrl: './campaign-list.page.html',
  styleUrl: '../campaigns.pages.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampaignListPage implements OnInit {
  private readonly campaignsClient = inject(CampaignsClient);
  readonly campaigns = signal<CampaignSummary[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.campaignsClient.list()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (campaigns) => this.campaigns.set(campaigns),
        error: (error) => this.error.set(apiErrorMessage(error, 'No se han podido cargar las campañas.')),
      });
  }
}
