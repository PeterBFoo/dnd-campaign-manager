import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize, switchMap } from 'rxjs';

import { apiErrorMessage } from '@shared/http/problem-details';

import { InvitationPreview } from '../api/invitation.contracts';
import { InvitationsClient } from '../api/invitations.client';
import { PASSWORD_VALIDATORS, passwordValidationMessage } from '../password/password-validation';
import { SessionStore } from '../session/session.store';

@Component({
  selector: 'dnd-invitation-acceptance',
  imports: [DatePipe, ReactiveFormsModule, RouterLink],
  templateUrl: './invitation-acceptance.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InvitationAcceptancePage implements OnInit {
  private readonly invitations = inject(InvitationsClient);
  private readonly session = inject(SessionStore);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private token = '';

  readonly preview = signal<InvitationPreview | null>(null);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly accepted = signal(false);
  readonly error = signal<string | null>(null);
  readonly accountForm = new FormGroup({
    displayName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2)] }),
    password: new FormControl('', { nonNullable: true, validators: PASSWORD_VALIDATORS }),
  });
  readonly loginForm = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  passwordError(): string | null {
    return passwordValidationMessage(this.accountForm.controls.password);
  }

  ngOnInit(): void {
    const parameters = new URLSearchParams(this.route.snapshot.fragment ?? '');
    this.token = parameters.get('token') ?? '';
    history.replaceState(null, '', location.pathname + location.search);
    if (!this.token) {
      this.loading.set(false);
      this.error.set('El enlace no contiene una invitación válida.');
      return;
    }

    this.invitations
      .preview(this.token)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (preview) => this.preview.set(preview),
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido comprobar la invitación.')),
      });
  }

  createAccountAndAccept(): void {
    if (this.accountForm.invalid || this.submitting()) {
      this.accountForm.markAllAsTouched();
      return;
    }

    const { displayName, password } = this.accountForm.getRawValue();
    this.accept(displayName, password);
  }

  loginAndAccept(): void {
    if (this.loginForm.invalid || this.submitting()) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    const { email, password } = this.loginForm.getRawValue();
    this.session
      .login(email, password)
      .pipe(
        switchMap(() => this.invitations.accept(this.token)),
        finalize(() => this.submitting.set(false)),
      )
      .subscribe({
        next: () => this.accepted.set(true),
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido aceptar la invitación.')),
      });
  }

  finish(): void {
    void this.router.navigate(['/']);
  }

  private accept(displayName?: string, password?: string): void {
    this.submitting.set(true);
    this.error.set(null);
    this.invitations
      .accept(this.token, displayName, password)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (acceptance) => {
          if (acceptance.accessToken && acceptance.expiresAt) {
            this.session.useAcceptedSession({
              accessToken: acceptance.accessToken,
              expiresAt: acceptance.expiresAt,
              user: acceptance.user,
            });
          }
          this.accepted.set(true);
        },
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido aceptar la invitación.')),
      });
  }
}
