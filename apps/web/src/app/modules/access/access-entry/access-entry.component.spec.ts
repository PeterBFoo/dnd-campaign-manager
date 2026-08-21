import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { IdentityClient } from '../api/identity.client';
import { SessionStore } from '../session/session.store';
import { AccessEntryComponent } from './access-entry.component';

describe('AccessEntryComponent', () => {
  it('logs in and routes platform administrators to invitation management', async () => {
    const user = { id: 'user-1', email: 'dm@example.com', displayName: 'DM', isPlatformAdmin: true };
    const sessionStub = {
      user: signal(null),
      login: vi.fn(() => of({ accessToken: 'token', expiresAt: '2099-01-01T00:00:00Z', user })),
    };
    const identityStub = { bootstrapStatus: vi.fn(() => of({ state: 'completed' })) };
    await TestBed.configureTestingModule({
      imports: [AccessEntryComponent],
      providers: [
        provideRouter([]),
        { provide: SessionStore, useValue: sessionStub },
        { provide: IdentityClient, useValue: identityStub },
      ],
    }).compileComponents();
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(AccessEntryComponent);
    fixture.detectChanges();
    fixture.componentInstance.loginForm.setValue({ email: 'dm@example.com', password: 'password' });

    fixture.componentInstance.login();

    expect(sessionStub.login).toHaveBeenCalledWith('dm@example.com', 'password');
    expect(navigate).toHaveBeenCalledWith(['/admin/invitations']);
  });

  it('shows the bootstrap entry when the platform requires it', async () => {
    const sessionStub = { user: signal(null), login: vi.fn() };
    const identityStub = { bootstrapStatus: vi.fn(() => of({ state: 'required' })) };
    await TestBed.configureTestingModule({
      imports: [AccessEntryComponent],
      providers: [
        provideRouter([]),
        { provide: SessionStore, useValue: sessionStub },
        { provide: IdentityClient, useValue: identityStub },
      ],
    }).compileComponents();
    const fixture = TestBed.createComponent(AccessEntryComponent);

    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Crea la administración inicial');
  });
});
