import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';

export interface PlatformStatus {
  service: string;
  status: 'operational' | 'degraded';
  environment: string;
  version: string;
  generatedAt: string;
  dependencies: {
    database: string;
    telemetry: string;
  };
}

@Injectable({ providedIn: 'root' })
export class PlatformStatusService {
  private readonly http = inject(HttpClient);

  readonly status = signal<PlatformStatus | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.http
      .get<PlatformStatus>('/api/v1/platform/status')
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
