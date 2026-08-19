export interface DndRuntimeConfig {
  apiBaseUrl?: string;
}

declare global {
  interface Window {
    __DND_CONFIG__?: DndRuntimeConfig;
  }
}

export function apiBaseUrl(): string {
  return window.__DND_CONFIG__?.apiBaseUrl?.replace(/\/$/, '') ?? '';
}
