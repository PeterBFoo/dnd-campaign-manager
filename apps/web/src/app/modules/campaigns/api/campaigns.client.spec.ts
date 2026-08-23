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
    expect(create.request.body).toEqual({ name: 'Mesa propia' });
    create.flush({});
  });

  it('keeps the campaign identifier in detail requests', () => {
    client.get('campaign-1').subscribe();

    const detail = http.expectOne('/api/v1/campaigns/campaign-1');
    expect(detail.request.method).toBe('GET');
    detail.flush({});
  });
});
