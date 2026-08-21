import { Injectable, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';

import { PlatformClient } from '../api/platform.client';
import { PlatformStatus } from '../api/platform.contracts';

@Injectable()
export class PlatformStatusStore {
  private readonly client = inject(PlatformClient);

  readonly status = signal<PlatformStatus | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.client
      .getStatus()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (status) => this.status.set(status),
        error: () => {
          this.status.set(null);
          this.error.set('La API todavía no está disponible. Levanta el stack para conectarla.');
        },
      });
  }
}
