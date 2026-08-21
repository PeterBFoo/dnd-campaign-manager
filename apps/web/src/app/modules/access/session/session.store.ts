import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, finalize, tap } from 'rxjs';

import { IdentityClient } from '../api/identity.client';
import { SessionResponse } from '../api/identity.contracts';

const sessionKey = 'dnd.user-session';

@Injectable()
export class SessionStore {
  private readonly identity = inject(IdentityClient);
  private readonly session = signal<SessionResponse | null>(this.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly authenticated = computed(() => this.user() !== null);

  accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  login(email: string, password: string): Observable<SessionResponse> {
    return this.identity.login(email, password).pipe(tap((session) => this.store(session)));
  }

  useAcceptedSession(session: SessionResponse): void {
    this.store(session);
  }

  logout(): Observable<void> {
    return this.identity.logout().pipe(finalize(() => this.clear()));
  }

  clear(): void {
    sessionStorage.removeItem(sessionKey);
    this.session.set(null);
  }

  private store(session: SessionResponse): void {
    sessionStorage.setItem(sessionKey, JSON.stringify(session));
    this.session.set(session);
  }

  private restore(): SessionResponse | null {
    const serialized = sessionStorage.getItem(sessionKey);
    if (!serialized) {
      return null;
    }

    try {
      const session = JSON.parse(serialized) as SessionResponse;
      if (!session.accessToken || new Date(session.expiresAt).getTime() <= Date.now()) {
        sessionStorage.removeItem(sessionKey);
        return null;
      }

      return session;
    } catch {
      sessionStorage.removeItem(sessionKey);
      return null;
    }
  }
}
