import { AuthenticatedUser } from './identity.contracts';

export interface InvitationPreview {
  state: 'valid' | 'invalid' | 'expired' | 'accepted' | 'revoked';
  kind: 'platform' | 'campaign' | null;
  recipientEmail: string | null;
  expiresAt: string | null;
  requiresAuthentication: boolean;
}

export interface InvitationAcceptance {
  user: AuthenticatedUser;
  accessToken: string | null;
  expiresAt: string | null;
  kind: 'platform' | 'campaign';
}

export interface InvitationSummary {
  id: string;
  kind: 'platform' | 'campaign';
  recipientEmail: string;
  campaignId: string | null;
  status: 'pending' | 'accepted' | 'expired' | 'revoked';
  deliveryStatus: 'pending' | 'sent' | 'failed' | 'discarded';
  issuedAt: string;
  expiresAt: string;
  lastSentAt: string | null;
}
