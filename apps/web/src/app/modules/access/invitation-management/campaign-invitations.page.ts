import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, finalize, of, startWith, switchMap } from 'rxjs';

import { apiErrorMessage } from '@shared/http/problem-details';

import { EligibleUser, InvitationSummary } from '../api/invitation.contracts';
import { InvitationsClient } from '../api/invitations.client';

@Component({
  selector: 'dnd-campaign-invitations',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './campaign-invitations.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CampaignInvitationsPage implements OnInit {
  private readonly invitationsClient = inject(InvitationsClient);
  private readonly campaignId = inject(ActivatedRoute).snapshot.paramMap.get('campaignId') ?? '';
  readonly invitations = signal<InvitationSummary[]>([]);
  readonly eligibleUsers = signal<EligibleUser[]>([]);
  readonly usersLoading = signal(true);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly search = new FormControl('', { nonNullable: true });

  ngOnInit(): void {
    this.load();
    this.search.valueChanges.pipe(
      startWith(this.search.value),
      debounceTime(250),
      distinctUntilChanged(),
      switchMap((query) => {
        this.usersLoading.set(true);
        this.error.set(null);
        return this.invitationsClient.eligibleCampaignUsers(
          this.campaignId,
          query.trim().length >= 2 ? query.trim() : undefined,
        ).pipe(
          catchError((error) => {
            this.fail(error);
            return of({ items: [], nextCursor: null });
          }),
          finalize(() => this.usersLoading.set(false)),
        );
      }),
    ).subscribe({
      next: (page) => this.eligibleUsers.set(page.items),
    });
  }

  issue(user: EligibleUser): void {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.clear();
    this.invitationsClient
      .issueCampaignUser(this.campaignId, user.userId)
      .pipe(finalize(() => this.busy.set(false)))
      .subscribe({
        next: () => {
          this.message.set('Invitación creada.');
          this.load();
          this.refreshEligibleUsers();
        },
        error: (error) => this.fail(error),
      });
  }

  resend(invitation: InvitationSummary): void {
    this.clear();
    this.invitationsClient.resendCampaign(this.campaignId, invitation.id).subscribe({
      next: () => {
        this.message.set('Invitación renovada.');
        this.load();
      },
      error: (error) => this.fail(error),
    });
  }

  revoke(invitation: InvitationSummary): void {
    this.clear();
    this.invitationsClient.revokeCampaign(this.campaignId, invitation.id).subscribe({
      next: () => {
        this.message.set('Invitación revocada.');
        this.load();
      },
      error: (error) => this.fail(error),
    });
  }

  private load(): void {
    this.loading.set(true);
    this.invitationsClient
      .listCampaign(this.campaignId)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (items) => this.invitations.set(items),
        error: (error) => this.fail(error),
      });
  }

  private refreshEligibleUsers(): void {
    const query = this.search.value.trim();
    this.usersLoading.set(true);
    this.invitationsClient
      .eligibleCampaignUsers(this.campaignId, query.length >= 2 ? query : undefined)
      .pipe(finalize(() => this.usersLoading.set(false)))
      .subscribe({
        next: (page) => this.eligibleUsers.set(page.items),
        error: (error) => this.fail(error),
      });
  }

  private clear(): void {
    this.message.set(null);
    this.error.set(null);
  }

  private fail(error: unknown): void {
    this.error.set(apiErrorMessage(error, 'No se ha podido completar la operación.'));
  }
}
