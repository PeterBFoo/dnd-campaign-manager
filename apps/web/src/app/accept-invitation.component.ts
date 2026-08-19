import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize, switchMap } from 'rxjs';

import { apiErrorMessage } from './api-error';
import { AuthService } from './auth.service';
import { IdentityApiService, InvitationPreview } from './identity-api.service';

@Component({
  selector: 'dnd-accept-invitation',
  imports: [DatePipe, ReactiveFormsModule, RouterLink],
  templateUrl: './accept-invitation.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AcceptInvitationComponent implements OnInit {
  private readonly identity = inject(IdentityApiService);
  private readonly auth = inject(AuthService);
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
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(12)] }),
  });
  readonly loginForm = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  ngOnInit(): void {
    const parameters = new URLSearchParams(this.route.snapshot.fragment ?? '');
    this.token = parameters.get('token') ?? '';
    history.replaceState(null, '', location.pathname + location.search);
    if (!this.token) {
      this.loading.set(false);
      this.error.set('El enlace no contiene una invitación válida.');
      return;
    }

    this.identity
      .previewInvitation(this.token)
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
    this.auth
      .login(email, password)
      .pipe(
        switchMap(() => this.identity.acceptInvitation(this.token)),
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
    this.identity
      .acceptInvitation(this.token, displayName, password)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: (acceptance) => {
          if (acceptance.accessToken && acceptance.expiresAt) {
            this.auth.useAcceptedSession({
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
