import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { CampaignsClient } from '@modules/campaigns';
import { CharactersClient } from '@modules/characters';

import { CombatClient } from './api/combat.client';
import { DmEncounter } from './api/combat.contracts';
import { COMBAT_ROUTES } from './combat.routes';
import { EncounterPage } from './encounter-page/encounter.page';

const dmEncounter: DmEncounter = {
  id: 'encounter-1', campaignId: 'campaign-1', name: 'Encuentro', status: 'active', round: 1,
  currentParticipantId: 'character-participant', tiesResolved: true, version: 4,
  createdAt: '2026-08-23T10:00:00Z', activatedAt: '2026-08-23T10:05:00Z', finishedAt: null,
  participants: [
    {
      id: 'character-participant', characterId: 'character-1', name: 'Exploradora', kind: 'character',
      armorClass: 16, initiative: 18, orderPosition: 0, quantity: 1, members: [],
      isCurrentTurn: true,
    },
    {
      id: 'enemy-1', characterId: null, name: 'Adversario', kind: 'enemy', armorClass: 14,
      initiative: 12, orderPosition: 1, quantity: 2, members: [
        { id: 'member-1', ordinal: 1, currentHitPoints: 20, maximumHitPoints: 20 },
        { id: 'member-2', ordinal: 2, currentHitPoints: 7, maximumHitPoints: 20 },
      ],
      isCurrentTurn: false,
    },
  ],
};

function providers(role: 'dm' | 'player', client: object, encounterId?: string) {
  return [
    provideRouter([]),
    { provide: CombatClient, useValue: client },
    { provide: CampaignsClient, useValue: { get: vi.fn(() => of({
      id: 'campaign-1', name: 'Mesa', role, adventureModuleId: null, createdAt: '2026-08-23T00:00:00Z',
    })) } },
    { provide: CharactersClient, useValue: { list: vi.fn(() => of([])) } },
    {
      provide: ActivatedRoute,
      useValue: { snapshot: { paramMap: convertToParamMap({ campaignId: 'campaign-1', encounterId }) } },
    },
  ];
}

describe('combat pages', () => {
  it('uses authenticated lazy routes for list and detail', () => {
    expect(COMBAT_ROUTES.map((route) => route.path)).toEqual([
      'campaigns/:campaignId/encounters',
      'campaigns/:campaignId/encounters/:encounterId',
    ]);
    expect(COMBAT_ROUTES.every((route) => route.canActivate?.length === 1)).toBe(true);
  });

  it('renders the private dm table with armor and hit points', async () => {
    const client = {
      list: vi.fn(() => of({ items: [] })),
      get: vi.fn(() => of(dmEncounter)),
    };
    await TestBed.configureTestingModule({
      imports: [EncounterPage],
      providers: providers('dm', client, 'encounter-1'),
    }).compileComponents();
    const fixture = TestBed.createComponent(EncounterPage);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Ronda 1');
    expect(fixture.nativeElement.textContent).toContain('CA');
    expect(fixture.nativeElement.textContent).toContain('20 / 20');
    expect(fixture.nativeElement.textContent).toContain('#2 · 7 / 20');
    expect(fixture.nativeElement.textContent).toContain('Siguiente turno');
  });

  it('polls the safe player view and cancels when destroyed', async () => {
    vi.useFakeTimers();
    const active = vi.fn(() => of({
      encounter: {
        id: 'encounter-1', name: 'Encuentro', round: 2, currentParticipantName: 'Exploradora',
        participants: [
          { name: 'Exploradora', kind: 'character', initiative: 18, orderPosition: 0, quantity: 1, isCurrentTurn: true },
          { name: 'Adversario', kind: 'enemy', initiative: 12, orderPosition: 1, quantity: 8, isCurrentTurn: false },
        ],
      },
    }));
    TestBed.configureTestingModule({
      imports: [EncounterPage],
      providers: providers('player', { active }),
    });
    const fixture = TestBed.createComponent(EncounterPage);
    fixture.detectChanges();
    await vi.advanceTimersByTimeAsync(0);
    fixture.detectChanges();

    expect(active).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).toContain('Ronda 2');
    expect(fixture.nativeElement.textContent).toContain('Turno actual');
    expect(fixture.nativeElement.textContent).not.toContain('Vida');
    expect(fixture.nativeElement.textContent).toContain('8');
    expect([...fixture.nativeElement.querySelectorAll('th')].map((value: HTMLElement) => value.textContent)).not.toContain('CA');

    await vi.advanceTimersByTimeAsync(5000);
    expect(active).toHaveBeenCalledTimes(2);
    fixture.destroy();
    await vi.advanceTimersByTimeAsync(5000);
    expect(active).toHaveBeenCalledTimes(2);
    vi.useRealTimers();
  });
});
