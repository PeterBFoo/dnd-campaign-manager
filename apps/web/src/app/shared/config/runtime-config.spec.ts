import { apiBaseUrl } from './runtime-config';

describe('runtime config', () => {
  afterEach(() => delete window.__DND_CONFIG__);

  it('uses a relative API URL by default', () => {
    expect(apiBaseUrl()).toBe('');
  });

  it('removes one trailing slash from the configured API URL', () => {
    window.__DND_CONFIG__ = { apiBaseUrl: 'https://api.example.test/' };

    expect(apiBaseUrl()).toBe('https://api.example.test');
  });
});
