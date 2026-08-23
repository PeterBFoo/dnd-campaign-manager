import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { JournalClient } from './journal.client';

describe('JournalClient', () => {
  let client: JournalClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), JournalClient],
    });
    client = TestBed.inject(JournalClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists entries with bounded cursor pagination', () => {
    client.list('campaign-1', 'opaque-cursor').subscribe();

    const request = http.expectOne((candidate) => candidate.url.endsWith(
      '/api/v1/campaigns/campaign-1/journal/entries',
    ));
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('cursor')).toBe('opaque-cursor');
    expect(request.request.params.get('limit')).toBe('20');
    request.flush({ items: [], nextCursor: null });
  });

  it('uses JSON writes for create, collaborative update and delete', () => {
    client.create('campaign-1', 'Pista').subscribe();
    const create = http.expectOne('/api/v1/campaigns/campaign-1/journal/entries');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ content: 'Pista' });
    create.flush({});

    client.update('campaign-1', 'entry-1', 'Pista editada').subscribe();
    const update = http.expectOne('/api/v1/campaigns/campaign-1/journal/entries/entry-1');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({ content: 'Pista editada' });
    update.flush({});

    client.delete('campaign-1', 'entry-1').subscribe();
    const remove = http.expectOne('/api/v1/campaigns/campaign-1/journal/entries/entry-1');
    expect(remove.request.method).toBe('DELETE');
    remove.flush(null);
  });
});
