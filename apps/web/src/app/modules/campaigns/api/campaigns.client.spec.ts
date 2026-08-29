import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { CampaignsClient } from './campaigns.client';

describe('CampaignsClient', () => {
  let client: CampaignsClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), CampaignsClient],
    });
    client = TestBed.inject(CampaignsClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uses the campaign collection for listing and creation', () => {
    client.list().subscribe();
    const list = http.expectOne('/api/v1/campaigns');
    expect(list.request.method).toBe('GET');
    list.flush([]);

    client.create('Mesa propia').subscribe();
    const create = http.expectOne('/api/v1/campaigns');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ name: 'Mesa propia', adventureModuleId: null });
    create.flush({});
  });

  it('keeps the campaign identifier in detail requests', () => {
    client.get('campaign-1').subscribe();

    const detail = http.expectOne('/api/v1/campaigns/campaign-1');
    expect(detail.request.method).toBe('GET');
    detail.flush({});
  });

  it('deletes the selected campaign resource', () => {
    client.delete('campaign-1').subscribe();

    const deletion = http.expectOne('/api/v1/campaigns/campaign-1');
    expect(deletion.request.method).toBe('DELETE');
    deletion.flush(null);
  });

  it('assigns and removes an adventure module with the expected version', () => {
    client.assignModule('campaign-1', 'module-1', 3).subscribe();
    const assignment = http.expectOne('/api/v1/campaigns/campaign-1/adventure-module');
    expect(assignment.request.method).toBe('PUT');
    expect(assignment.request.body).toEqual({ adventureModuleId: 'module-1', expectedVersion: 3 });
    assignment.flush({});

    client.removeModule('campaign-1', 4).subscribe();
    const removal = http.expectOne('/api/v1/campaigns/campaign-1/adventure-module?expectedVersion=4');
    expect(removal.request.method).toBe('DELETE');
    removal.flush({});
  });
});
