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

export interface BootstrapStatus {
  state: 'required' | 'completed';
}
