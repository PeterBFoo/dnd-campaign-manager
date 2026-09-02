import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AdventureLocationsClient } from './adventure-locations.client';
import { AdventureLocation } from './adventure-locations.contracts';

describe('AdventureLocationsClient', () => {
  let client: AdventureLocationsClient; let http: HttpTestingController;
  const location = { id: 'location-1', moduleId: 'module-1', name: 'Villa', description: null, detailMapId: null, detailMap: null, pointsOfInterest: [], placements: [], chapters: [], version: 3 } satisfies AdventureLocation;
  beforeEach(() => { TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), AdventureLocationsClient] }); client = TestBed.inject(AdventureLocationsClient); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());

  it('lists locations for an admin module', () => { client.listAdmin('module-1').subscribe(value => expect(value).toEqual([location])); http.expectOne('/api/v1/admin/adventure-modules/module-1/locations').flush([location]); });
  it('sends normalized placement coordinates with the current version', () => { client.setPlacement('module-1', location, 'map-1', .25, .75).subscribe(); const request = http.expectOne('/api/v1/admin/adventure-modules/module-1/locations/location-1/placements/map-1'); expect(request.request.method).toBe('PUT'); expect(request.request.body).toEqual({ x: .25, y: .75, expectedVersion: 3 }); });
  it('uses the campaign read-only route', () => { client.listCampaign('campaign-1').subscribe(); http.expectOne('/api/v1/campaigns/campaign-1/adventure/locations'); });
});
