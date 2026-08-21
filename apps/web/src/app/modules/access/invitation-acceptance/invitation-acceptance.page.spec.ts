import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { InvitationsClient } from '../api/invitations.client';
import { SessionStore } from '../session/session.store';
import { InvitationAcceptancePage } from './invitation-acceptance.page';

describe('InvitationAcceptancePage', () => {
  it('previews the fragment token and stores a session returned on acceptance', async () => {
    const invitationsStub = {
      preview: vi.fn(() => of({
        state: 'valid',
        kind: 'campaign',
        recipientEmail: 'player@example.com',
        expiresAt: '2099-01-01T00:00:00Z',
        requiresAuthentication: false,
      })),
      accept: vi.fn(() => of({
        user: { id: 'user-1', email: 'player@example.com', displayName: 'Player', isPlatformAdmin: false },
        accessToken: 'accepted-session',
        expiresAt: '2099-01-01T00:00:00Z',
        kind: 'campaign',
      })),
    };
    const sessionStub = { login: vi.fn(), useAcceptedSession: vi.fn() };
    await TestBed.configureTestingModule({
      imports: [InvitationAcceptancePage],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { fragment: 'token=invitation-token' } } },
        { provide: InvitationsClient, useValue: invitationsStub },
        { provide: SessionStore, useValue: sessionStub },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(InvitationAcceptancePage);
    fixture.detectChanges();
    fixture.componentInstance.accountForm.setValue({
      displayName: 'Player',
      password: 'Strong-password-1!',
    });

    fixture.componentInstance.createAccountAndAccept();

    expect(invitationsStub.preview).toHaveBeenCalledWith('invitation-token');
    expect(invitationsStub.accept).toHaveBeenCalledWith(
      'invitation-token',
      'Player',
      'Strong-password-1!',
    );
    expect(sessionStub.useAcceptedSession).toHaveBeenCalledWith({
      accessToken: 'accepted-session',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'player@example.com', displayName: 'Player', isPlatformAdmin: false },
    });
    expect(fixture.componentInstance.accepted()).toBe(true);
  });

  it('does not call the API when the fragment has no token', async () => {
    const invitationsStub = { preview: vi.fn(), accept: vi.fn() };
    await TestBed.configureTestingModule({
      imports: [InvitationAcceptancePage],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { fragment: null } } },
        { provide: InvitationsClient, useValue: invitationsStub },
        { provide: SessionStore, useValue: {} },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(InvitationAcceptancePage);

    fixture.detectChanges();

    expect(invitationsStub.preview).not.toHaveBeenCalled();
    expect(fixture.componentInstance.loading()).toBe(false);
    expect(fixture.componentInstance.error()).toBe('El enlace no contiene una invitación válida.');
  });
});
