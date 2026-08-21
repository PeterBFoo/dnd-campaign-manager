import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { InvitationsClient } from './invitations.client';

describe('InvitationsClient', () => {
  let client: InvitationsClient;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), InvitationsClient],
    });
    client = TestBed.inject(InvitationsClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('previews and accepts an invitation token', () => {
    client.preview('invite-token').subscribe();
    const preview = http.expectOne('/api/v1/invitations/preview');
    expect(preview.request.method).toBe('POST');
    expect(preview.request.body).toEqual({ token: 'invite-token' });
    preview.flush({ state: 'valid', kind: 'campaign', recipientEmail: 'p@example.com', expiresAt: null, requiresAuthentication: false });

    client.accept('invite-token', 'Player', 'Strong-password-1!').subscribe();
    const accept = http.expectOne('/api/v1/invitations/accept');
    expect(accept.request.method).toBe('POST');
    expect(accept.request.body).toEqual({
      token: 'invite-token',
      displayName: 'Player',
      password: 'Strong-password-1!',
    });
    accept.flush({});
  });

  it('uses the platform invitation endpoints for every command', () => {
    client.listPlatform().subscribe();
    const list = http.expectOne('/api/v1/platform/invitations');
    expect(list.request.method).toBe('GET');
    list.flush([]);

    client.issuePlatform('new@example.com').subscribe();
    const issue = http.expectOne('/api/v1/platform/invitations');
    expect(issue.request.method).toBe('POST');
    expect(issue.request.body).toEqual({ email: 'new@example.com' });
    issue.flush({});

    client.resendPlatform('invitation-1').subscribe();
    const resend = http.expectOne('/api/v1/platform/invitations/invitation-1/resend');
    expect(resend.request.method).toBe('POST');
    expect(resend.request.body).toEqual({});
    resend.flush({});

    client.revokePlatform('invitation-1').subscribe();
    const revoke = http.expectOne('/api/v1/platform/invitations/invitation-1');
    expect(revoke.request.method).toBe('DELETE');
    revoke.flush(null);
  });

  it('keeps campaignId in every campaign invitation endpoint', () => {
    client.listCampaign('campaign-1').subscribe();
    const list = http.expectOne('/api/v1/campaigns/campaign-1/invitations');
    expect(list.request.method).toBe('GET');
    list.flush([]);

    client.issueCampaign('campaign-1', 'new@example.com').subscribe();
    const issue = http.expectOne('/api/v1/campaigns/campaign-1/invitations');
    expect(issue.request.method).toBe('POST');
    expect(issue.request.body).toEqual({ email: 'new@example.com' });
    issue.flush({});

    client.resendCampaign('campaign-1', 'invitation-1').subscribe();
    const resend = http.expectOne('/api/v1/campaigns/campaign-1/invitations/invitation-1/resend');
    expect(resend.request.method).toBe('POST');
    resend.flush({});

    client.revokeCampaign('campaign-1', 'invitation-1').subscribe();
    const revoke = http.expectOne('/api/v1/campaigns/campaign-1/invitations/invitation-1');
    expect(revoke.request.method).toBe('DELETE');
    revoke.flush(null);
  });
});
