import { ACCESS_ROUTES } from './modules/access/access.routes';
import { routes } from './app.routes';

describe('application routes', () => {
  it('keeps home, Access lazy routes and wildcard in the composition root', () => {
    expect(routes.map((route) => route.path)).toEqual(['', '', '', '**']);
    expect(routes[0]?.pathMatch).toBe('full');
    expect(routes[0]?.loadComponent).toBeTypeOf('function');
    expect(routes[1]?.loadChildren).toBeTypeOf('function');
    expect(routes[2]?.loadChildren).toBeTypeOf('function');
    expect(routes[3]?.redirectTo).toBe('');
  });

  it('keeps every existing Access path and guard', () => {
    expect(ACCESS_ROUTES.map((route) => route.path)).toEqual([
      'bootstrap',
      'accept-invitation',
      'admin/invitations',
      'campaigns/:campaignId/invitations',
    ]);
    expect(ACCESS_ROUTES[2]?.canActivate).toHaveLength(1);
    expect(ACCESS_ROUTES[3]?.canActivate).toHaveLength(1);
    expect(ACCESS_ROUTES.every((route) => route.loadComponent)).toBe(true);
  });
});
