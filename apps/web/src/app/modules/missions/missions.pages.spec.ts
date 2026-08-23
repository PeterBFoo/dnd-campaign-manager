import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { SessionStore } from '@modules/access';
import { CampaignsClient } from '@modules/campaigns';
import { CharactersClient } from '@modules/characters';

import { Mission } from './api/mission.contracts';
import { MissionsClient } from './api/missions.client';
import { MissionPage } from './mission-page/mission.page';
import { MISSIONS_ROUTES } from './missions.routes';

const mission: Mission = {
  id: 'mission-1',
  campaignId: 'campaign-1',
  title: 'Objetivo compartido',
  description: 'Descripción genérica',
  status: 'active',
  isMain: true,
  authorType: 'player',
  authorCharacterId: 'character-1',
  authorDisplayName: 'Exploradora',
  createdAt: '2026-08-23T10:00:00Z',
  updatedAt: null,
  canDelete: false,
};

function providers(role: 'dm' | 'player', active: boolean, client: object) {
  return [
    provideRouter([]),
    { provide: MissionsClient, useValue: client },
    { provide: CampaignsClient, useValue: { get: vi.fn(() => of({
      id: 'campaign-1', name: 'Mesa', role, adventureModuleId: null, createdAt: '2026-08-23T00:00:00Z',
    })) } },
    { provide: CharactersClient, useValue: { list: vi.fn(() => of(active ? [{
      id: 'character-1', campaignId: 'campaign-1', ownerUserId: 'player-1', ownerDisplayName: 'Jugador',
      name: 'Exploradora', armorClass: 16, initiative: 3, imageUrl: '/images/default-character.svg',
      isActive: true, createdAt: '2026-08-23T00:00:00Z',
    }] : [])) } },
    { provide: SessionStore, useValue: { user: () => ({ id: 'player-1' }) } },
    {
      provide: ActivatedRoute,
      useValue: { snapshot: { paramMap: convertToParamMap({ campaignId: 'campaign-1' }) } },
    },
  ];
}

describe('missions pages', () => {
  it('uses an authenticated lazy route', () => {
    expect(MISSIONS_ROUTES[0]?.path).toBe('campaigns/:campaignId/missions');
    expect(MISSIONS_ROUTES[0]?.canActivate).toHaveLength(1);
    expect(MISSIONS_ROUTES[0]?.loadComponent).toBeTypeOf('function');
  });

  it('creates without date controls and shows the returned principal', async () => {
    const created = { ...mission, canDelete: true };
    const list = vi.fn()
      .mockReturnValueOnce(of({ items: [] }))
      .mockReturnValueOnce(of({ items: [created] }));
    const client = { list, create: vi.fn(() => of(created)) };
    await TestBed.configureTestingModule({
      imports: [MissionPage],
      providers: providers('player', true, client),
    }).compileComponents();
    const fixture = TestBed.createComponent(MissionPage);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('input[type="date"]')).toBeNull();
    fixture.componentInstance.form.setValue({
      title: 'Objetivo compartido', description: 'Descripción genérica', status: 'active', isMain: true,
    });
    fixture.componentInstance.submit();
    fixture.detectChanges();

    expect(client.create).toHaveBeenCalledWith('campaign-1', {
      title: 'Objetivo compartido', description: 'Descripción genérica', isMain: true,
    });
    expect(fixture.nativeElement.textContent).toContain('Misión principal');
    expect(fixture.nativeElement.textContent).toContain('Exploradora');
    expect(fixture.nativeElement.textContent).toContain('Eliminar');
  });

  it('allows collaborative editing but hides deletion for another player', async () => {
    const client = { list: vi.fn(() => of({ items: [mission] })) };
    await TestBed.configureTestingModule({
      imports: [MissionPage],
      providers: providers('player', false, client),
    }).compileComponents();
    const fixture = TestBed.createComponent(MissionPage);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Necesitas un personaje activo');
    expect(fixture.nativeElement.textContent).toContain('Editar');
    expect(fixture.nativeElement.textContent).not.toContain('Eliminar');
    fixture.componentInstance.beginEdit(mission);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Editar misión');
  });

  it('shows deletion when the API authorizes the dm', async () => {
    const client = { list: vi.fn(() => of({ items: [{ ...mission, canDelete: true }] })) };
    await TestBed.configureTestingModule({
      imports: [MissionPage],
      providers: providers('dm', false, client),
    }).compileComponents();
    const fixture = TestBed.createComponent(MissionPage);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Nueva misión');
    expect(fixture.nativeElement.textContent).toContain('Eliminar');
    expect(fixture.nativeElement.querySelector('input[type="date"]')).toBeNull();
  });
});
