import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { PlatformClient } from './platform.client';

describe('PlatformClient', () => {
  it('gets the platform status without retaining presentation state', () => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), PlatformClient],
    });
    const client = TestBed.inject(PlatformClient);
    const http = TestBed.inject(HttpTestingController);

    client.getStatus().subscribe((status) => expect(status.status).toBe('operational'));
    const request = http.expectOne('/api/v1/platform/status');
    expect(request.request.method).toBe('GET');
    request.flush({
      service: 'api',
      status: 'operational',
      environment: 'Test',
      version: '1',
      generatedAt: '2026-08-22T00:00:00Z',
      dependencies: { database: 'ready', telemetry: 'ready' },
    });
    http.verify();
  });
});
