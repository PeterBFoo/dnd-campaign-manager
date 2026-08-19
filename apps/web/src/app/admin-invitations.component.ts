import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';

import { apiErrorMessage } from './api-error';
import { InvitationApiService, InvitationSummary } from './invitation-api.service';

@Component({
  selector: 'dnd-admin-invitations',
  imports: [DatePipe, ReactiveFormsModule],
  templateUrl: './admin-invitations.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AdminInvitationsComponent implements OnInit {
  private readonly api = inject(InvitationApiService);
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
    this.api
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
    this.api.revokePlatform(invitation.id).subscribe({
      next: () => {
        this.notice.set('Invitación revocada.');
        this.load();
      },
      error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido revocar la invitación.')),
    });
  }

  resend(invitation: InvitationSummary): void {
    this.clearMessages();
    this.api.resendPlatform(invitation.id).subscribe({
      next: () => {
        this.notice.set('Se ha generado una nueva invitación y la anterior ha quedado revocada.');
        this.load();
      },
      error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido reenviar la invitación.')),
    });
  }

  private load(): void {
    this.loading.set(true);
    this.api
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
