import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { apiErrorMessage } from '@shared/http/problem-details';

import { IdentityClient } from '../api/identity.client';
import { SessionStore } from '../session/session.store';

@Component({
  selector: 'dnd-access-entry',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './access-entry.component.html',
  styleUrl: './access-entry.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccessEntryComponent implements OnInit {
  readonly session = inject(SessionStore);
  private readonly identity = inject(IdentityClient);
  private readonly router = inject(Router);

  readonly bootstrapRequired = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly loginForm = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  ngOnInit(): void {
    this.identity.bootstrapStatus().subscribe({
      next: ({ state }) => this.bootstrapRequired.set(state === 'required'),
      error: () => this.bootstrapRequired.set(false),
    });
  }

  login(): void {
    if (this.loginForm.invalid || this.submitting()) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    const { email, password } = this.loginForm.getRawValue();
    this.session
      .login(email, password)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: ({ user }) => {
          void this.router.navigate(user.isPlatformAdmin ? ['/admin/invitations'] : ['/']);
        },
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido iniciar sesión.')),
      });
  }
}
