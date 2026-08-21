import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';

import { IdentityClient } from './api/identity.client';
import { SessionStore } from './session/session.store';

export function provideAccess(): EnvironmentProviders {
  return makeEnvironmentProviders([IdentityClient, SessionStore]);
}
