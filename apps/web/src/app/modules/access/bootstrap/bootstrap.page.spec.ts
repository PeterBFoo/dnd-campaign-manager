import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { IdentityClient } from '../api/identity.client';
import { BootstrapPage } from './bootstrap.page';

describe('BootstrapPage', () => {
  it('submits the complete form and navigates home', async () => {
    const identityStub = {
      bootstrap: vi.fn(() => of({
        id: 'user-1',
        email: 'dm@example.com',
        displayName: 'DM',
        isPlatformAdmin: true,
      })),
    };
    await TestBed.configureTestingModule({
      imports: [BootstrapPage],
      providers: [
        provideRouter([]),
        { provide: IdentityClient, useValue: identityStub },
      ],
    }).compileComponents();
    const router = TestBed.inject(Router);
    const navigate = vi.spyOn(router, 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(BootstrapPage);
    fixture.componentInstance.form.setValue({
      token: 'bootstrap-token',
      displayName: 'DM',
      email: 'dm@example.com',
      password: 'Strong-password-1!',
    });

    fixture.componentInstance.submit();

    expect(identityStub.bootstrap).toHaveBeenCalledWith(
      'bootstrap-token',
      'dm@example.com',
      'DM',
      'Strong-password-1!',
    );
    expect(navigate).toHaveBeenCalledWith(['/'], { state: { bootstrapCompleted: true } });
    expect(fixture.componentInstance.submitting()).toBe(false);
  });
});
