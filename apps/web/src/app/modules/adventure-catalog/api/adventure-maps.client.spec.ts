import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AdventureMapsClient } from './adventure-maps.client';
import { AdventureMap } from './adventure-maps.contracts';

describe('AdventureMapsClient', () => {
  let client: AdventureMapsClient; let http: HttpTestingController;
  const map = { id: 'map-1', moduleId: 'module-1', name: 'Región', description: null, hasImage: false, imageUrl: null, width: null, height: null, chapters: [], createdAt: '', updatedAt: '', version: 4 } satisfies AdventureMap;
  beforeEach(() => { TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), AdventureMapsClient] }); client = TestBed.inject(AdventureMapsClient); http = TestBed.inject(HttpTestingController); });
  afterEach(() => http.verify());

  it('uses module-scoped admin routes and expected versions', () => {
    client.update('module-1', map, 'Región nueva', '').subscribe();
    const update = http.expectOne('/api/v1/admin/adventure-modules/module-1/maps/map-1');
    expect(update.request.method).toBe('PUT'); expect(update.request.body.expectedVersion).toBe(4); update.flush(map);
    client.setChapter('module-1', map, 'chapter-1', true).subscribe();
    const link = http.expectOne('/api/v1/admin/adventure-modules/module-1/maps/map-1/chapters/chapter-1?expectedVersion=4');
    expect(link.request.method).toBe('PUT'); link.flush(map);
  });

  it('keeps DM reads under the campaign and requests images lazily', () => {
    client.listCampaign('campaign-1').subscribe();
    http.expectOne('/api/v1/campaigns/campaign-1/adventure/maps').flush([]);
    client.imageCampaign('campaign-1', 'map-1').subscribe();
    const image = http.expectOne('/api/v1/campaigns/campaign-1/adventure/maps/map-1/image');
    expect(image.request.method).toBe('GET'); expect(image.request.responseType).toBe('blob'); image.flush(new Blob());
  });
});
