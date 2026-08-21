import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AcceptInvitationComponent } from './accept-invitation.component';
import { AuthService } from './auth.service';
import { BootstrapComponent } from './bootstrap.component';
import { IdentityApiService } from './identity-api.service';

describe('password flows', () => {
  it('shows the length error when bootstrap is submitted with a short password', async () => {
    const identityStub = { bootstrap: vi.fn() };
    await TestBed.configureTestingModule({
      imports: [BootstrapComponent],
      providers: [
        provideRouter([]),
        { provide: IdentityApiService, useValue: identityStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(BootstrapComponent);
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
    const identityStub = {
      previewInvitation: vi.fn(() => of({
        state: 'valid',
        kind: 'campaign',
        recipientEmail: 'player@example.com',
        expiresAt: '2026-08-23T00:00:00Z',
        requiresAuthentication: false,
      })),
      acceptInvitation: vi.fn(),
    };
    const authStub = {
      login: vi.fn(),
      useAcceptedSession: vi.fn(),
    };
    await TestBed.configureTestingModule({
      imports: [AcceptInvitationComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { snapshot: { fragment: 'token=invitation-token' } } },
        { provide: IdentityApiService, useValue: identityStub },
        { provide: AuthService, useValue: authStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(AcceptInvitationComponent);
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
    expect(identityStub.acceptInvitation).not.toHaveBeenCalled();
  });
});
