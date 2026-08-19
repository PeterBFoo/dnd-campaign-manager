import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { apiErrorMessage } from './api-error';
import { AuthService } from './auth.service';
import { IdentityApiService } from './identity-api.service';
import { PlatformStatusService } from './platform-status.service';

@Component({
  selector: 'dnd-landing',
  imports: [DatePipe, ReactiveFormsModule, RouterLink],
  templateUrl: './landing.component.html',
  styleUrl: './landing.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LandingComponent implements OnInit {
  readonly platform = inject(PlatformStatusService);
  readonly auth = inject(AuthService);
  private readonly identity = inject(IdentityApiService);
  private readonly router = inject(Router);

  readonly bootstrapRequired = signal(false);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly loginForm = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  ngOnInit(): void {
    this.platform.load();
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
    this.auth
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
