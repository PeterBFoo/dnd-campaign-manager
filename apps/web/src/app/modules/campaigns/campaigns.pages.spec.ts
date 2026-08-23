import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, convertToParamMap, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { CharactersClient } from '@modules/characters';

import { CampaignsClient } from './api/campaigns.client';
import { CampaignCreatePage } from './campaign-create/campaign-create.page';
import { CampaignDetailPage } from './campaign-detail/campaign-detail.page';
import { CampaignListPage } from './campaign-list/campaign-list.page';

const campaign = {
  id: 'campaign-1',
  name: 'Mesa propia',
  role: 'dm' as const,
  adventureModuleId: null,
  createdAt: '2026-08-23T00:00:00Z',
};

describe('campaign pages', () => {
  it('shows accessible campaigns with their role and module state', async () => {
    const clientStub = { list: vi.fn(() => of([campaign])) };
    await TestBed.configureTestingModule({
      imports: [CampaignListPage],
      providers: [provideRouter([]), { provide: CampaignsClient, useValue: clientStub }],
    }).compileComponents();
    const fixture = TestBed.createComponent(CampaignListPage);

    fixture.detectChanges();

    expect(clientStub.list).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.textContent).toContain('Mesa propia');
    expect(fixture.nativeElement.textContent).toContain('Sin módulo');
    expect(fixture.nativeElement.textContent).toContain('DM');
  });

  it('validates and creates a campaign before navigating to its detail', async () => {
    const clientStub = { create: vi.fn(() => of(campaign)) };
    await TestBed.configureTestingModule({
      imports: [CampaignCreatePage],
      providers: [provideRouter([]), { provide: CampaignsClient, useValue: clientStub }],
    }).compileComponents();
    const fixture = TestBed.createComponent(CampaignCreatePage);
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);

    fixture.componentInstance.submit();
    expect(clientStub.create).not.toHaveBeenCalled();

    fixture.componentInstance.form.setValue({ name: 'Mesa propia' });
    fixture.componentInstance.submit();

    expect(clientStub.create).toHaveBeenCalledWith('Mesa propia');
    expect(navigate).toHaveBeenCalledWith(['/campaigns', 'campaign-1']);
  });

  it('shows invitation management only for a campaign directed by the actor', async () => {
    const clientStub = { get: vi.fn(() => of(campaign)) };
    const charactersStub = { list: vi.fn(() => of([
      {
        id: 'character-active', campaignId: 'campaign-1', ownerUserId: 'player-1', ownerDisplayName: 'Jugador',
        name: 'Exploradora', armorClass: 16, initiative: 3, imageUrl: '/images/default-character.svg',
        isActive: true, createdAt: '2026-08-23T00:00:00Z',
      },
      {
        id: 'character-inactive', campaignId: 'campaign-1', ownerUserId: 'player-1', ownerDisplayName: 'Jugador',
        name: 'Guerrera', armorClass: 18, initiative: 1, imageUrl: '/images/default-character.svg',
        isActive: false, createdAt: '2026-08-23T00:00:00Z',
      },
    ])) };
    await TestBed.configureTestingModule({
      imports: [CampaignDetailPage],
      providers: [
        provideRouter([]),
        { provide: CampaignsClient, useValue: clientStub },
        { provide: CharactersClient, useValue: charactersStub },
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { paramMap: convertToParamMap({ campaignId: 'campaign-1' }) } },
        },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(CampaignDetailPage);

    fixture.detectChanges();

    expect(clientStub.get).toHaveBeenCalledWith('campaign-1');
    expect(charactersStub.list).toHaveBeenCalledWith('campaign-1');
    expect(fixture.nativeElement.textContent).toContain('Invitar jugadores');
    expect(fixture.nativeElement.textContent).toContain('Exploradora');
    expect(fixture.nativeElement.textContent).not.toContain('Guerrera');
    expect(fixture.nativeElement.textContent).toContain('Gestionar personajes');
  });
});
