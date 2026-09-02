import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';

import { SessionStore } from '@modules/access';

import { AppComponent } from './app.component';

describe('AppComponent', () => {
  it('renders the application shell', async () => {
    const sessionStub = {
      user: signal(null),
      logout: vi.fn(() => of(undefined)),
    };

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: SessionStore, useValue: sessionStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Campaign Keeper');
    expect(fixture.nativeElement.textContent).toContain('ASP.NET Core 10');
    expect(fixture.nativeElement.textContent).toContain('Acceso por invitación');
  });

  it('renders campaign and platform navigation for an authenticated administrator', async () => {
    const sessionStub = {
      user: signal({ id: 'user-1', displayName: 'Pere', email: 'pere@example.test', isPlatformAdmin: true }),
      logout: vi.fn(() => of(undefined)),
    };

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([
          { path: 'campaigns/:campaignId', component: AppComponent },
          { path: '**', component: AppComponent },
        ]),
        { provide: SessionStore, useValue: sessionStub },
      ],
    }).compileComponents();

    const router = TestBed.inject(Router);
    await router.navigateByUrl('/campaigns/campaign-1');
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    const content = fixture.nativeElement.textContent;
    expect(content).toContain('Campaign Keeper');
    expect(content).toContain('Campaña activa');
    expect(content).toContain('Personajes');
    expect(content).toContain('Módulos');
    expect(content).toContain('Administrador de plataforma');
  });
});
