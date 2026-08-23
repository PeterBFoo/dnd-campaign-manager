import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of } from 'rxjs';

import { InvitationsClient } from '../api/invitations.client';
import { CampaignInvitationsPage } from './campaign-invitations.page';
import { PlatformInvitationsPage } from './platform-invitations.page';

const invitation = {
  id: 'invitation-1',
  kind: 'platform' as const,
  recipientEmail: 'player@example.com',
  campaignId: null,
  status: 'pending' as const,
  deliveryStatus: 'sent' as const,
  issuedAt: '2026-08-22T00:00:00Z',
  expiresAt: '2026-08-29T00:00:00Z',
  lastSentAt: '2026-08-22T00:00:00Z',
};

describe('invitation management pages', () => {
  it('lists and issues platform invitations', async () => {
    const clientStub = {
      listPlatform: vi.fn(() => of([invitation])),
      issuePlatform: vi.fn(() => of(invitation)),
      resendPlatform: vi.fn(() => of(invitation)),
      revokePlatform: vi.fn(() => of(undefined)),
    };
    await TestBed.configureTestingModule({
      imports: [PlatformInvitationsPage],
      providers: [{ provide: InvitationsClient, useValue: clientStub }],
    }).compileComponents();
    const fixture = TestBed.createComponent(PlatformInvitationsPage);
    fixture.detectChanges();
    fixture.componentInstance.form.setValue({ email: 'new@example.com' });

    fixture.componentInstance.issue();

    expect(clientStub.listPlatform).toHaveBeenCalledTimes(2);
    expect(clientStub.issuePlatform).toHaveBeenCalledWith('new@example.com');
    expect(fixture.componentInstance.notice()).toContain('Invitación creada');
  });

  it('keeps campaignId in campaign invitation operations', async () => {
    const campaignInvitation = { ...invitation, kind: 'campaign' as const, campaignId: 'campaign-1' };
    const clientStub = {
      listCampaign: vi.fn(() => of([campaignInvitation])),
      eligibleCampaignUsers: vi.fn(() => of({
        items: [{ userId: 'user-1', displayName: 'New Player', maskedEmail: 'ne***@example.com' }],
        nextCursor: null,
      })),
      issueCampaignUser: vi.fn(() => of(campaignInvitation)),
      resendCampaign: vi.fn(() => of(campaignInvitation)),
      revokeCampaign: vi.fn(() => of(undefined)),
    };
    await TestBed.configureTestingModule({
      imports: [CampaignInvitationsPage],
      providers: [
        { provide: InvitationsClient, useValue: clientStub },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ campaignId: 'campaign-1' }) } } },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(CampaignInvitationsPage);
    fixture.detectChanges();

    fixture.componentInstance.issue({
      userId: 'user-1',
      displayName: 'New Player',
      maskedEmail: 'ne***@example.com',
    });
    fixture.componentInstance.resend(campaignInvitation);
    fixture.componentInstance.revoke(campaignInvitation);

    expect(clientStub.listCampaign).toHaveBeenCalledWith('campaign-1');
    expect(clientStub.issueCampaignUser).toHaveBeenCalledWith('campaign-1', 'user-1');
    expect(clientStub.resendCampaign).toHaveBeenCalledWith('campaign-1', 'invitation-1');
    expect(clientStub.revokeCampaign).toHaveBeenCalledWith('campaign-1', 'invitation-1');
  });
});
