import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { apiErrorMessage } from './api-error';
import { IdentityApiService } from './identity-api.service';

@Component({
  selector: 'dnd-bootstrap',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './bootstrap.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BootstrapComponent {
  private readonly identity = inject(IdentityApiService);
  private readonly router = inject(Router);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly form = new FormGroup({
    token: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    displayName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(2)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(12)] }),
  });

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
