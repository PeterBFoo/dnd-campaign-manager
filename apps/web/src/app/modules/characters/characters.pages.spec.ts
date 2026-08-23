import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { CampaignsClient } from '@modules/campaigns';

import { CharactersClient } from './api/characters.client';
import { CharacterFormPage } from './character-form/character-form.page';

describe('character pages', () => {
  it('validates required game values and creates a player character', async () => {
    const character = {
      id: 'character-1', campaignId: 'campaign-1', ownerUserId: 'player-1', ownerDisplayName: 'Jugador',
      name: 'Exploradora', armorClass: 16, initiative: 3, imageUrl: '/images/default-character.svg',
      isActive: true, createdAt: '2026-08-23T00:00:00Z',
    };
    const characters = { create: vi.fn(() => of(character)) };
    const campaigns = { get: vi.fn(() => of({
      id: 'campaign-1', name: 'Mesa', role: 'player', adventureModuleId: null, createdAt: '2026-08-23T00:00:00Z',
    })) };
    await TestBed.configureTestingModule({
      imports: [CharacterFormPage],
      providers: [
        provideRouter([]),
        { provide: CharactersClient, useValue: characters },
        { provide: CampaignsClient, useValue: campaigns },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ campaignId: 'campaign-1' }) } } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(CharacterFormPage);
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    fixture.detectChanges();

    fixture.componentInstance.submit();
    expect(characters.create).not.toHaveBeenCalled();
    fixture.componentInstance.form.patchValue({ name: 'Exploradora', armorClass: 16, initiative: 3 });
    fixture.componentInstance.submit();

    expect(characters.create).toHaveBeenCalledWith('campaign-1', expect.objectContaining({
      name: 'Exploradora', armorClass: 16, initiative: 3,
    }));
    expect(navigate).toHaveBeenCalledWith(['/campaigns', 'campaign-1', 'characters']);
  });

  it('loads accepted players for the DM owner selector', async () => {
    const characters = { owners: vi.fn(() => of([{ userId: 'player-1', displayName: 'Jugador Uno' }])) };
    const campaigns = { get: vi.fn(() => of({
      id: 'campaign-1', name: 'Mesa', role: 'dm', adventureModuleId: null, createdAt: '2026-08-23T00:00:00Z',
    })) };
    await TestBed.configureTestingModule({
      imports: [CharacterFormPage],
      providers: [
        provideRouter([]),
        { provide: CharactersClient, useValue: characters },
        { provide: CampaignsClient, useValue: campaigns },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ campaignId: 'campaign-1' }) } } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(CharacterFormPage);

    fixture.detectChanges();

    expect(characters.owners).toHaveBeenCalledWith('campaign-1');
    expect(fixture.nativeElement.textContent).toContain('Jugador vinculado');
    expect(fixture.nativeElement.textContent).toContain('Jugador Uno');
  });
});
