import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';

import { AppComponent } from './app.component';
import { AuthService } from './auth.service';

describe('AppComponent', () => {
  it('renders the application shell', async () => {
    const authStub = {
      user: signal(null),
      logout: vi.fn(() => of(undefined)),
    };

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: AuthService, useValue: authStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Campaign Companion');
    expect(fixture.nativeElement.textContent).toContain('ASP.NET Core 10');
    expect(fixture.nativeElement.textContent).toContain('Acceso por invitación');
  });
});
