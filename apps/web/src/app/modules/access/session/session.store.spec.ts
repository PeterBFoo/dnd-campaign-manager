import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';

import { IdentityClient } from '../api/identity.client';
import { SessionResponse } from '../api/identity.contracts';
import { SessionStore } from './session.store';

const validSession: SessionResponse = {
  accessToken: 'session-token',
  expiresAt: '2099-01-01T00:00:00Z',
  user: {
    id: 'user-1',
    email: 'player@example.com',
    displayName: 'Player',
    isPlatformAdmin: false,
  },
};

describe('SessionStore', () => {
  let identityStub: { login: ReturnType<typeof vi.fn>; logout: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    sessionStorage.clear();
    identityStub = {
      login: vi.fn(() => of(validSession)),
      logout: vi.fn(() => of(undefined)),
    };
  });

  afterEach(() => sessionStorage.clear());

  function createStore(): SessionStore {
    TestBed.configureTestingModule({
      providers: [
        SessionStore,
        { provide: IdentityClient, useValue: identityStub },
      ],
    });
    return TestBed.inject(SessionStore);
  }

  it('restores a valid session', () => {
    sessionStorage.setItem('dnd.user-session', JSON.stringify(validSession));

    const store = createStore();

    expect(store.user()?.email).toBe('player@example.com');
    expect(store.authenticated()).toBe(true);
    expect(store.accessToken()).toBe('session-token');
  });

  it.each([
    JSON.stringify({ ...validSession, accessToken: '' }),
    JSON.stringify({ ...validSession, expiresAt: '2000-01-01T00:00:00Z' }),
    '{not-json',
  ])('rejects an invalid stored session', (serialized) => {
    sessionStorage.setItem('dnd.user-session', serialized);

    const store = createStore();

    expect(store.authenticated()).toBe(false);
    expect(sessionStorage.getItem('dnd.user-session')).toBeNull();
  });

  it('stores the session returned by login', () => {
    const store = createStore();

    store.login('player@example.com', 'password').subscribe();

    expect(identityStub.login).toHaveBeenCalledWith('player@example.com', 'password');
    expect(JSON.parse(sessionStorage.getItem('dnd.user-session') ?? '{}')).toEqual(validSession);
    expect(store.user()).toEqual(validSession.user);
  });

  it('clears local state when remote logout fails', () => {
    sessionStorage.setItem('dnd.user-session', JSON.stringify(validSession));
    identityStub.logout.mockReturnValue(throwError(() => new Error('network')));
    const store = createStore();

    store.logout().subscribe({ error: () => undefined });

    expect(store.user()).toBeNull();
    expect(sessionStorage.getItem('dnd.user-session')).toBeNull();
  });
});
