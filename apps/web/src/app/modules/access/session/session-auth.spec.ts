import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, provideRouter } from '@angular/router';
import { signal } from '@angular/core';

import { authInterceptor } from './auth.interceptor';
import { authenticatedGuard } from './authenticated.guard';
import { platformAdminGuard } from './platform-admin.guard';
import { SessionStore } from './session.store';

describe('Access authentication adapters', () => {
  it('redirects visitors and allows authenticated users', () => {
    const sessionStub = {
      authenticated: signal(false),
      user: signal(null),
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: SessionStore, useValue: sessionStub },
      ],
    });
    const router = TestBed.inject(Router);

    const visitorResult = TestBed.runInInjectionContext(() => authenticatedGuard(
      {} as ActivatedRouteSnapshot,
      {} as RouterStateSnapshot,
    ));
    expect(router.serializeUrl(visitorResult as ReturnType<Router['createUrlTree']>)).toBe('/');

    sessionStub.authenticated.set(true);
    const authenticatedResult = TestBed.runInInjectionContext(() => authenticatedGuard(
      {} as ActivatedRouteSnapshot,
      {} as RouterStateSnapshot,
    ));
    expect(authenticatedResult).toBe(true);
  });

  it('allows only platform administrators through the admin guard', () => {
    const sessionStub = {
      authenticated: signal(true),
      user: signal({ id: '1', email: 'dm@example.com', displayName: 'DM', isPlatformAdmin: false }),
    };
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: SessionStore, useValue: sessionStub },
      ],
    });

    const playerResult = TestBed.runInInjectionContext(() => platformAdminGuard(
      {} as ActivatedRouteSnapshot,
      {} as RouterStateSnapshot,
    ));
    expect(playerResult).not.toBe(true);

    sessionStub.user.set({ id: '1', email: 'dm@example.com', displayName: 'DM', isPlatformAdmin: true });
    const adminResult = TestBed.runInInjectionContext(() => platformAdminGuard(
      {} as ActivatedRouteSnapshot,
      {} as RouterStateSnapshot,
    ));
    expect(adminResult).toBe(true);
  });

  it('adds the bearer token only when a session token exists', () => {
    const sessionStub = { accessToken: vi.fn<() => string | null>(() => 'session-token') };
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        { provide: SessionStore, useValue: sessionStub },
      ],
    });
    const client = TestBed.inject(HttpClient);
    const http = TestBed.inject(HttpTestingController);

    client.get('/resource').subscribe();
    const authenticated = http.expectOne('/resource');
    expect(authenticated.request.headers.get('Authorization')).toBe('Bearer session-token');
    authenticated.flush({});

    sessionStub.accessToken.mockReturnValue(null);
    client.get('/public').subscribe();
    const anonymous = http.expectOne('/public');
    expect(anonymous.request.headers.has('Authorization')).toBe(false);
    anonymous.flush({});
    http.verify();
  });
});
