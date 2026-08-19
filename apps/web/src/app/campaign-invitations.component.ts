import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { finalize } from 'rxjs';

import { apiErrorMessage } from './api-error';
import { InvitationApiService, InvitationSummary } from './invitation-api.service';

@Component({
  selector: 'dnd-campaign-invitations',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './campaign-invitations.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampaignInvitationsComponent implements OnInit {
  private readonly api = inject(InvitationApiService);
  private readonly campaignId = inject(ActivatedRoute).snapshot.paramMap.get('campaignId') ?? '';
  readonly invitations = signal<InvitationSummary[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
  });

  ngOnInit(): void { this.load(); }

  issue(): void {
    if (this.form.invalid || this.busy()) { this.form.markAllAsTouched(); return; }
    this.busy.set(true); this.clear();
    this.api.issueCampaign(this.campaignId, this.form.controls.email.value)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({ next: () => { this.form.reset(); this.message.set('Invitación creada.'); this.load(); }, error: (error) => this.fail(error) });
  }

  resend(invitation: InvitationSummary): void {
    this.clear();
    this.api.resendCampaign(this.campaignId, invitation.id).subscribe({ next: () => { this.message.set('Invitación renovada.'); this.load(); }, error: (error) => this.fail(error) });
  }

  revoke(invitation: InvitationSummary): void {
    this.clear();
    this.api.revokeCampaign(this.campaignId, invitation.id).subscribe({ next: () => { this.message.set('Invitación revocada.'); this.load(); }, error: (error) => this.fail(error) });
  }

  private load(): void {
    this.loading.set(true);
    this.api.listCampaign(this.campaignId).pipe(finalize(() => this.loading.set(false))).subscribe({ next: (items) => this.invitations.set(items), error: (error) => this.fail(error) });
  }

  private clear(): void { this.message.set(null); this.error.set(null); }
  private fail(error: unknown): void { this.error.set(apiErrorMessage(error, 'No se ha podido completar la operación.')); }
}
