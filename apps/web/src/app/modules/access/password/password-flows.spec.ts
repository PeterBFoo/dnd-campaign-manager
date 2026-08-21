import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { IdentityClient } from '../api/identity.client';
import { InvitationsClient } from '../api/invitations.client';
import { BootstrapPage } from '../bootstrap/bootstrap.page';
import { InvitationAcceptancePage } from '../invitation-acceptance/invitation-acceptance.page';
import { SessionStore } from '../session/session.store';

describe('password flows', () => {
  it('shows the length error when bootstrap is submitted with a short password', async () => {
    const identityStub = { bootstrap: vi.fn() };
    await TestBed.configureTestingModule({
      imports: [BootstrapPage],
      providers: [
        provideRouter([]),
        { provide: IdentityClient, useValue: identityStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(BootstrapPage);
    fixture.componentInstance.form.setValue({
      token: 'bootstrap-token',
      displayName: 'Admin',
      email: 'admin@example.com',
      password: 'Aa1!short',
    });

    fixture.componentInstance.submit();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#bootstrap-password-error')?.textContent).toContain(
      'La contraseña debe contener entre 12 y 128 caracteres.',
    );
    expect(identityStub.bootstrap).not.toHaveBeenCalled();
  });

  it('shows the length error when accepting an invitation with a new account', async () => {
    const invitationsStub = {
      preview: vi.fn(() => of({
        state: 'valid',
        kind: 'campaign',
        recipientEmail: 'player@example.com',
        expiresAt: '2026-08-23T00:00:00Z',
        requiresAuthentication: false,
      })),
      accept: vi.fn(),
    };
    const sessionStub = {
      login: vi.fn(),
      useAcceptedSession: vi.fn(),
    };
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
      password: 'Aa1!short',
    });

    fixture.componentInstance.createAccountAndAccept();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#invitation-password-error')?.textContent).toContain(
      'La contraseña debe contener entre 12 y 128 caracteres.',
    );
    expect(invitationsStub.accept).not.toHaveBeenCalled();
  });
});
