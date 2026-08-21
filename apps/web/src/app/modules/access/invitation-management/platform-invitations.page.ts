import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';

import { apiErrorMessage } from '@shared/http/problem-details';

import { InvitationSummary } from '../api/invitation.contracts';
import { InvitationsClient } from '../api/invitations.client';

@Component({
  selector: 'dnd-platform-invitations',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './platform-invitations.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlatformInvitationsPage implements OnInit {
  private readonly invitationsClient = inject(InvitationsClient);
  readonly invitations = signal<InvitationSummary[]>([]);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
  });

  ngOnInit(): void {
    this.load();
  }

  issue(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.clearMessages();
    this.invitationsClient
      .issuePlatform(this.form.controls.email.value)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          this.form.reset();
          this.notice.set('Invitación creada. El envío se procesará en segundo plano.');
          this.load();
        },
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido crear la invitación.')),
      });
  }

  revoke(invitation: InvitationSummary): void {
    this.clearMessages();
    this.invitationsClient.revokePlatform(invitation.id).subscribe({
      next: () => {
        this.notice.set('Invitación revocada.');
        this.load();
      },
      error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido revocar la invitación.')),
    });
  }

  resend(invitation: InvitationSummary): void {
    this.clearMessages();
    this.invitationsClient.resendPlatform(invitation.id).subscribe({
      next: () => {
        this.notice.set('Se ha generado una nueva invitación y la anterior ha quedado revocada.');
        this.load();
      },
      error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido reenviar la invitación.')),
    });
  }

  private load(): void {
    this.loading.set(true);
    this.invitationsClient
      .listPlatform()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (invitations) => this.invitations.set(invitations),
        error: (error) => this.error.set(apiErrorMessage(error, 'No se han podido cargar las invitaciones.')),
      });
  }

  private clearMessages(): void {
    this.error.set(null);
    this.notice.set(null);
  }
}
