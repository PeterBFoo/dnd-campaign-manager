import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { PlatformClient } from '../api/platform.client';
import { PlatformStatusStore } from './platform-status.store';

const status = {
  service: 'api',
  status: 'operational' as const,
  environment: 'Test',
  version: '1',
  generatedAt: '2026-08-22T00:00:00Z',
  dependencies: { database: 'ready', telemetry: 'ready' },
};

describe('PlatformStatusStore', () => {
  it('loads and exposes platform status', () => {
    TestBed.configureTestingModule({
      providers: [
        PlatformStatusStore,
        { provide: PlatformClient, useValue: { getStatus: () => of(status) } },
      ],
    });
    const store = TestBed.inject(PlatformStatusStore);

    store.load();

    expect(store.loading()).toBe(false);
    expect(store.status()).toEqual(status);
    expect(store.error()).toBeNull();
  });

  it('publishes the existing public error on failure', () => {
    TestBed.configureTestingModule({
      providers: [
        PlatformStatusStore,
        { provide: PlatformClient, useValue: { getStatus: () => throwError(() => new Error('network')) } },
      ],
    });
    const store = TestBed.inject(PlatformStatusStore);

    store.load();

    expect(store.loading()).toBe(false);
    expect(store.status()).toBeNull();
    expect(store.error()).toBe('La API todavía no está disponible. Levanta el stack para conectarla.');
  });
});
