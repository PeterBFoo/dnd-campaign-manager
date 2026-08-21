import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { apiBaseUrl } from '@shared/config/runtime-config';

import { PlatformStatus } from './platform.contracts';

@Injectable()
export class PlatformClient {
  private readonly http = inject(HttpClient);

  getStatus(): Observable<PlatformStatus> {
    return this.http.get<PlatformStatus>(`${apiBaseUrl()}/api/v1/platform/status`);
  }
}
