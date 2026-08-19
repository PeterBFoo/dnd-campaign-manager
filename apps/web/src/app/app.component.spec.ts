import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';

import { AppComponent } from './app.component';
import { PlatformStatusService } from './platform-status.service';

describe('AppComponent', () => {
  it('renders the platform foundation', async () => {
    const platformStub = {
      status: signal(null),
      loading: signal(false),
      error: signal('API unavailable'),
      load: vi.fn(),
    };

    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [{ provide: PlatformStatusService, useValue: platformStub }],
    }).compileComponents();

    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('La memoria de la aventura');
    expect(fixture.nativeElement.textContent).toContain('ASP.NET Core 10');
    expect(platformStub.load).toHaveBeenCalledOnce();
  });
});
