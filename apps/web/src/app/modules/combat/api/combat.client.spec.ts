import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { CombatClient } from './combat.client';

describe('CombatClient', () => {
  let client: CombatClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), CombatClient],
    });
    client = TestBed.inject(CombatClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    delete window.__DND_CONFIG__;
  });

  it('uses the configured API origin in production', () => {
    window.__DND_CONFIG__ = { apiBaseUrl: 'https://api.example.test/' };

    client.create('campaign-1', 'Encuentro de prueba').subscribe();

    const creation = http.expectOne('https://api.example.test/api/v1/campaigns/campaign-1/encounters');
    expect(creation.request.method).toBe('POST');
    expect(creation.request.body).toEqual({ name: 'Encuentro de prueba' });
    creation.flush({});
  });

  it('separates dm listings from the safe active projection', () => {
    client.list('campaign-1').subscribe();
    const list = http.expectOne('/api/v1/campaigns/campaign-1/encounters');
    expect(list.request.method).toBe('GET');
    list.flush({ items: [] });

    client.active('campaign-1').subscribe();
    const active = http.expectOne('/api/v1/campaigns/campaign-1/encounters/active');
    expect(active.request.method).toBe('GET');
    active.flush({ encounter: null });
  });

  it('sends versioned preparation and combat commands', () => {
    client.addEnemy('campaign-1', 'encounter-1', {
      name: 'Adversarios', initiative: 12, armorClass: 14, maximumHitPoints: 20, quantity: 8, expectedVersion: 3,
    }).subscribe();
    const enemy = http.expectOne('/api/v1/campaigns/campaign-1/encounters/encounter-1/enemies');
    expect(enemy.request.method).toBe('POST');
    expect(enemy.request.body).toEqual({
      name: 'Adversarios', initiative: 12, armorClass: 14, maximumHitPoints: 20, quantity: 8, expectedVersion: 3,
    });
    enemy.flush({});

    client.adjustHitPoints('campaign-1', 'encounter-1', 'enemy-1', 'member-1', 'damage', 5, 4).subscribe();
    const hitPoints = http.expectOne(
      '/api/v1/campaigns/campaign-1/encounters/encounter-1/enemies/enemy-1/members/member-1/hit-points',
    );
    expect(hitPoints.request.body).toEqual({ kind: 'damage', amount: 5, expectedVersion: 4 });
    hitPoints.flush({});

    client.advance('campaign-1', 'encounter-1', 5).subscribe();
    const advance = http.expectOne(
      '/api/v1/campaigns/campaign-1/encounters/encounter-1/turns/advance',
    );
    expect(advance.request.body).toEqual({ expectedVersion: 5 });
    advance.flush({});

    client.deleteEncounter('campaign-1', 'encounter-1', 6).subscribe();
    const deletion = http.expectOne(
      '/api/v1/campaigns/campaign-1/encounters/encounter-1?expectedVersion=6',
    );
    expect(deletion.request.method).toBe('DELETE');
    deletion.flush(null);
  });
});
