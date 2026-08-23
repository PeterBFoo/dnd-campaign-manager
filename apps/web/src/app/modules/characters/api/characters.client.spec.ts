import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { CharactersClient } from './characters.client';

describe('CharactersClient', () => {
  let client: CharactersClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), CharactersClient],
    });
    client = TestBed.inject(CharactersClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('creates characters as multipart data including an uploaded image', () => {
    const image = new File([new Uint8Array([0xff, 0xd8, 0xff])], 'portrait.jpg', { type: 'image/jpeg' });
    client.create('campaign-1', {
      name: 'Exploradora', armorClass: 16, initiative: 3, ownerUserId: 'player-1', image,
    }).subscribe();

    const request = http.expectOne('/api/v1/campaigns/campaign-1/characters');
    expect(request.request.method).toBe('POST');
    const body = request.request.body as FormData;
    expect(body.get('name')).toBe('Exploradora');
    expect(body.get('armorClass')).toBe('16');
    expect(body.get('ownerUserId')).toBe('player-1');
    expect(body.get('image')).toBe(image);
    request.flush({});
  });

  it('uses explicit endpoints for update, activation and deletion', () => {
    client.update('campaign-1', 'character-1', {
      name: 'Bardo', armorClass: 14, initiative: 5, removeImage: true,
    }).subscribe();
    const update = http.expectOne('/api/v1/campaigns/campaign-1/characters/character-1');
    expect(update.request.method).toBe('PUT');
    expect((update.request.body as FormData).get('removeImage')).toBe('true');
    update.flush({});

    client.activate('campaign-1', 'character-1').subscribe();
    const activate = http.expectOne('/api/v1/campaigns/campaign-1/characters/character-1/active');
    expect(activate.request.method).toBe('PUT');
    activate.flush({});

    client.delete('campaign-1', 'character-1').subscribe();
    const remove = http.expectOne('/api/v1/campaigns/campaign-1/characters/character-1');
    expect(remove.request.method).toBe('DELETE');
    remove.flush(null);
  });
});
