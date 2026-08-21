import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { apiErrorMessage } from '@shared/http/problem-details';

import { IdentityClient } from '../api/identity.client';
import { PASSWORD_VALIDATORS, passwordValidationMessage } from '../password/password-validation';

@Component({
  selector: 'dnd-bootstrap',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './bootstrap.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BootstrapPage {
  private readonly identity = inject(IdentityClient);
  private readonly router = inject(Router);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly form = new FormGroup({
    token: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    displayName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: PASSWORD_VALIDATORS }),
  });

  passwordError(): string | null {
    return passwordValidationMessage(this.form.controls.password);
  }

  submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    const values = this.form.getRawValue();
    this.identity
      .bootstrap(values.token, values.email, values.displayName, values.password)
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => void this.router.navigate(['/'], { state: { bootstrapCompleted: true } }),
        error: (error) => this.error.set(apiErrorMessage(error, 'No se ha podido completar el alta inicial.')),
      });
  }
}
