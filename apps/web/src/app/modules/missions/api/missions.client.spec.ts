import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { MissionsClient } from './missions.client';

describe('MissionsClient', () => {
  let client: MissionsClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), MissionsClient],
    });
    client = TestBed.inject(MissionsClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists and writes missions without functional date fields', () => {
    client.list('campaign-1').subscribe();
    const list = http.expectOne('/api/v1/campaigns/campaign-1/missions');
    expect(list.request.method).toBe('GET');
    list.flush({ items: [] });

    client.create('campaign-1', { title: 'Objetivo', description: null, isMain: true }).subscribe();
    const create = http.expectOne('/api/v1/campaigns/campaign-1/missions');
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual({ title: 'Objetivo', description: null, isMain: true });
    expect(create.request.body).not.toHaveProperty('acceptedOn');
    expect(create.request.body).not.toHaveProperty('dueOn');
    create.flush({});

    client.update('campaign-1', 'mission-1', {
      title: 'Objetivo común', description: 'Texto', status: 'completed',
    }).subscribe();
    const update = http.expectOne('/api/v1/campaigns/campaign-1/missions/mission-1');
    expect(update.request.method).toBe('PUT');
    expect(update.request.body).toEqual({
      title: 'Objetivo común', description: 'Texto', status: 'completed',
    });
    update.flush({});
  });

  it('uses explicit main and deletion endpoints', () => {
    client.setMain('campaign-1', 'mission-1').subscribe();
    const setMain = http.expectOne('/api/v1/campaigns/campaign-1/missions/mission-1/main');
    expect(setMain.request.method).toBe('PUT');
    expect(setMain.request.body).toBeNull();
    setMain.flush({});

    client.clearMain('campaign-1', 'mission-1').subscribe();
    const clearMain = http.expectOne('/api/v1/campaigns/campaign-1/missions/mission-1/main');
    expect(clearMain.request.method).toBe('DELETE');
    clearMain.flush(null);

    client.delete('campaign-1', 'mission-1').subscribe();
    const remove = http.expectOne('/api/v1/campaigns/campaign-1/missions/mission-1');
    expect(remove.request.method).toBe('DELETE');
    remove.flush(null);
  });
});
