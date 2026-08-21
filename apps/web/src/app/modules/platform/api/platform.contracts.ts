export interface PlatformStatus {
  service: string;
  status: 'operational' | 'degraded';
  environment: string;
  version: string;
  generatedAt: string;
  dependencies: {
    database: string;
    telemetry: string;
  };
}
