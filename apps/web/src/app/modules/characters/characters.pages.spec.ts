import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { SessionStore } from '@modules/access';
import { CampaignsClient } from '@modules/campaigns';

import { CharactersClient } from './api/characters.client';
import { ActiveCharactersPanelComponent } from './active-characters-panel/active-characters-panel.component';
import { CharacterFormPage } from './character-form/character-form.page';
import { CharacterListPage } from './character-list/character-list.page';

describe('character pages', () => {
  const activeCharacter = {
    id: 'character-active', campaignId: 'campaign-1', ownerUserId: 'player-1', ownerDisplayName: 'Jugador',
    name: 'Exploradora', armorClass: 16, initiative: 3, imageUrl: '/images/default-character.svg',
    isActive: true, createdAt: '2026-08-23T00:00:00Z',
  };
  const inactiveCharacter = {
    id: 'character-inactive', campaignId: 'campaign-1', ownerUserId: 'player-1', ownerDisplayName: 'Jugador',
    name: 'Guerrera', armorClass: 18, initiative: 1, imageUrl: '/images/default-character.svg',
    isActive: false, createdAt: '2026-08-23T00:00:00Z',
  };

  it('shows only active characters in the campaign summary', async () => {
    const characters = { list: vi.fn(() => of([activeCharacter, inactiveCharacter])) };
    await TestBed.configureTestingModule({
      imports: [ActiveCharactersPanelComponent],
      providers: [provideRouter([]), { provide: CharactersClient, useValue: characters }],
    }).compileComponents();
    const fixture = TestBed.createComponent(ActiveCharactersPanelComponent);
    fixture.componentRef.setInput('campaignId', 'campaign-1');
    fixture.componentRef.setInput('role', 'player');

    fixture.detectChanges();

    expect(characters.list).toHaveBeenCalledWith('campaign-1');
    expect(fixture.nativeElement.textContent).toContain('Exploradora');
    expect(fixture.nativeElement.textContent).not.toContain('Guerrera');
    expect(fixture.nativeElement.textContent).toContain('Gestionar mis personajes');
  });

  it('limits a player management page to their own characters', async () => {
    const otherCharacter = {
      ...activeCharacter,
      id: 'character-other',
      ownerUserId: 'player-2',
      ownerDisplayName: 'Otro jugador',
      name: 'Bardo ajeno',
    };
    const characters = { list: vi.fn(() => of([inactiveCharacter, otherCharacter])) };
    const campaigns = { get: vi.fn(() => of({
      id: 'campaign-1', name: 'Mesa', role: 'player', adventureModuleId: null, createdAt: '2026-08-23T00:00:00Z',
    })) };
    const session = { user: () => ({ id: 'player-1' }) };
    await TestBed.configureTestingModule({
      imports: [CharacterListPage],
      providers: [
        provideRouter([]),
        { provide: CharactersClient, useValue: characters },
        { provide: CampaignsClient, useValue: campaigns },
        { provide: SessionStore, useValue: session },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ campaignId: 'campaign-1' }) } } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(CharacterListPage);

    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Guerrera');
    expect(fixture.nativeElement.textContent).not.toContain('Bardo ajeno');
  });

  it('keeps the full management roster visible to the DM', async () => {
    const otherCharacter = {
      ...activeCharacter,
      id: 'character-other',
      ownerUserId: 'player-2',
      ownerDisplayName: 'Otro jugador',
      name: 'Bardo vinculado',
    };
    const characters = { list: vi.fn(() => of([inactiveCharacter, otherCharacter])) };
    const campaigns = { get: vi.fn(() => of({
      id: 'campaign-1', name: 'Mesa', role: 'dm', adventureModuleId: null, createdAt: '2026-08-23T00:00:00Z',
    })) };
    const session = { user: () => ({ id: 'dm-1' }) };
    await TestBed.configureTestingModule({
      imports: [CharacterListPage],
      providers: [
        provideRouter([]),
        { provide: CharactersClient, useValue: characters },
        { provide: CampaignsClient, useValue: campaigns },
        { provide: SessionStore, useValue: session },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ campaignId: 'campaign-1' }) } } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(CharacterListPage);

    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Guerrera');
    expect(fixture.nativeElement.textContent).toContain('Bardo vinculado');
  });

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
