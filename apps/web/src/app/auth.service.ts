import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, finalize, tap } from 'rxjs';

import { apiBaseUrl } from './runtime-config';

export interface AuthenticatedUser {
  id: string;
  email: string;
  displayName: string;
  isPlatformAdmin: boolean;
}

export interface SessionResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthenticatedUser;
}

interface StoredSession extends SessionResponse {}

const sessionKey = 'dnd.user-session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = signal<StoredSession | null>(this.restore());

  readonly user = computed(() => this.session()?.user ?? null);
  readonly authenticated = computed(() => this.user() !== null);

  accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  login(email: string, password: string): Observable<SessionResponse> {
    return this.http
      .post<SessionResponse>(`${apiBaseUrl()}/api/v1/identity/login`, { email, password })
      .pipe(tap((session) => this.store(session)));
  }

  useAcceptedSession(session: SessionResponse): void {
    this.store(session);
  }

  logout(): Observable<void> {
    return this.http
      .post<void>(`${apiBaseUrl()}/api/v1/identity/logout`, {})
      .pipe(finalize(() => this.clear()));
  }

  clear(): void {
    sessionStorage.removeItem(sessionKey);
    this.session.set(null);
  }

  private store(session: SessionResponse): void {
    sessionStorage.setItem(sessionKey, JSON.stringify(session));
    this.session.set(session);
  }

  private restore(): StoredSession | null {
    const serialized = sessionStorage.getItem(sessionKey);
    if (!serialized) {
      return null;
    }

    try {
      const session = JSON.parse(serialized) as StoredSession;
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
