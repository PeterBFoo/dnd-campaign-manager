import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { IdentityClient } from './identity.client';

describe('IdentityClient', () => {
  let client: IdentityClient;
  let http: HttpTestingController;

  beforeEach(() => {
    window.__DND_CONFIG__ = { apiBaseUrl: 'https://api.example.test/' };
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), IdentityClient],
    });
    client = TestBed.inject(IdentityClient);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    delete window.__DND_CONFIG__;
  });

  it('gets bootstrap status from the configured API base URL', () => {
    client.bootstrapStatus().subscribe((result) => expect(result.state).toBe('required'));

    const request = http.expectOne('https://api.example.test/api/v1/identity/bootstrap');
    expect(request.request.method).toBe('GET');
    request.flush({ state: 'required' });
  });

  it('posts the complete bootstrap contract', () => {
    client.bootstrap('token', 'dm@example.com', 'Dungeon Master', 'Strong-password-1!').subscribe();

    const request = http.expectOne('https://api.example.test/api/v1/identity/bootstrap');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      token: 'token',
      email: 'dm@example.com',
      displayName: 'Dungeon Master',
      password: 'Strong-password-1!',
    });
    request.flush({ id: 'user-1', email: 'dm@example.com', displayName: 'Dungeon Master', isPlatformAdmin: true });
  });

  it('posts login and logout without changing their contracts', () => {
    client.login('player@example.com', 'password').subscribe();
    const login = http.expectOne('https://api.example.test/api/v1/identity/login');
    expect(login.request.method).toBe('POST');
    expect(login.request.body).toEqual({ email: 'player@example.com', password: 'password' });
    login.flush({
      accessToken: 'session-token',
      expiresAt: '2099-01-01T00:00:00Z',
      user: { id: 'user-1', email: 'player@example.com', displayName: 'Player', isPlatformAdmin: false },
    });

    client.logout().subscribe();
    const logout = http.expectOne('https://api.example.test/api/v1/identity/logout');
    expect(logout.request.method).toBe('POST');
    expect(logout.request.body).toEqual({});
    logout.flush(null);
  });
});
