import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { SessionStore } from '@modules/access';
import { CampaignsClient } from '@modules/campaigns';
import { CharactersClient } from '@modules/characters';

import { JournalClient } from './api/journal.client';
import { JournalEntry } from './api/journal.contracts';
import { JournalPage } from './journal-page/journal.page';

const originalEntry: JournalEntry = {
  id: 'entry-1',
  campaignId: 'campaign-1',
  authorCharacterId: 'character-1',
  authorCharacterName: 'Exploradora',
  content: 'Pista original',
  createdAt: '2026-08-23T10:00:00Z',
  updatedAt: null,
  canEdit: true,
  canDelete: false,
};

function providers(role: 'dm' | 'player', active: boolean, journal: object) {
  return [
    provideRouter([]),
    { provide: JournalClient, useValue: journal },
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

describe('journal pages', () => {
  it('shows original authorship and lets another player edit without delete permission', async () => {
    const updated = { ...originalEntry, content: 'Pista compartida', updatedAt: '2026-08-23T11:00:00Z' };
    const journal = {
      list: vi.fn(() => of({ items: [originalEntry], nextCursor: null })),
      update: vi.fn(() => of(updated)),
    };
    await TestBed.configureTestingModule({
      imports: [JournalPage],
      providers: providers('player', true, journal),
    }).compileComponents();
    const fixture = TestBed.createComponent(JournalPage);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Introducida por Exploradora');
    expect(fixture.nativeElement.textContent).toContain('Editar');
    expect(fixture.nativeElement.textContent).not.toContain('Eliminar');

    fixture.componentInstance.beginEdit(originalEntry);
    fixture.componentInstance.content.setValue('Pista compartida');
    fixture.componentInstance.submit();
    fixture.detectChanges();

    expect(journal.update).toHaveBeenCalledWith('campaign-1', 'entry-1', 'Pista compartida');
    expect(fixture.nativeElement.textContent).toContain('Introducida por Exploradora');
    expect(fixture.nativeElement.textContent).toContain('Pista compartida');
  });

  it('allows creation with an active character and prepends the response', async () => {
    const created = { ...originalEntry, id: 'entry-new', content: 'Entrada nueva', canDelete: true };
    const journal = {
      list: vi.fn(() => of({ items: [], nextCursor: null })),
      create: vi.fn(() => of(created)),
    };
    await TestBed.configureTestingModule({
      imports: [JournalPage],
      providers: providers('player', true, journal),
    }).compileComponents();
    const fixture = TestBed.createComponent(JournalPage);
    fixture.detectChanges();

    const textarea = fixture.nativeElement.querySelector('textarea') as HTMLTextAreaElement;
    textarea.value = 'Entrada nueva';
    textarea.dispatchEvent(new Event('input', { bubbles: true }));
    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    fixture.detectChanges();

    expect(journal.create).toHaveBeenCalledWith('campaign-1', 'Entrada nueva');
    expect(fixture.nativeElement.textContent).toContain('Entrada nueva');
    expect(fixture.nativeElement.textContent).toContain('Eliminar');
  });

  it('shows read-only state to the dm and active-character guidance to a player', async () => {
    const dmJournal = { list: vi.fn(() => of({ items: [{
      ...originalEntry, canEdit: false, canDelete: false,
    }], nextCursor: null })) };
    await TestBed.configureTestingModule({
      imports: [JournalPage],
      providers: providers('dm', false, dmJournal),
    }).compileComponents();
    const dmFixture = TestBed.createComponent(JournalPage);
    dmFixture.detectChanges();

    expect(dmFixture.nativeElement.textContent).toContain('Solo lectura');
    expect(dmFixture.nativeElement.textContent).not.toContain('Editar');

    TestBed.resetTestingModule();
    const playerJournal = { list: vi.fn(() => of({ items: [], nextCursor: null })) };
    await TestBed.configureTestingModule({
      imports: [JournalPage],
      providers: providers('player', false, playerJournal),
    }).compileComponents();
    const playerFixture = TestBed.createComponent(JournalPage);
    playerFixture.detectChanges();

    expect(playerFixture.nativeElement.textContent).toContain('Necesitas un personaje activo');
    expect(playerFixture.nativeElement.textContent).toContain('Gestionar mis personajes');
  });
});
