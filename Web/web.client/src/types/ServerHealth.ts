export type ServerStatus = 'Online' | 'Degraded' | 'Offline';

export interface ServerProgram {
  name: string;
  /** Null means the count is not checked. */
  expected: number | null;
  /** Null when the PC has not reported diagnostics. */
  running: number | null;
  notResponding: number;
}

export interface ServerLogFile {
  prefix: string;
  lastWriteAt: string | null;
}

export interface ServerStartupTask {
  name: string;
  found: boolean;
  lastRunAt: string | null;
  lastResult: number | null;
}

export interface ServerDiagnostics {
  receivedAt: string;
  collectedAt: string;
  hostname: string | null;
  clientVersion: string | null;
  osVersion: string | null;
  lastBootAt: string | null;
  logDriveFreeBytes: number | null;
  healthPingIntervalMinutes: number;
  lastTelemetrySentAt: string | null;
  telemetrySendFailuresLastHour: number;
  logs: ServerLogFile[];
  startupTasks: ServerStartupTask[];
}

export interface ServerHealth {
  beaconID: number;
  beaconName: string;
  status: ServerStatus;
  reasons: string[];
  lastCheckIn: string | null;
  offlineThresholdMinutes: number;
  hasDiagnostics: boolean;
  subdivisions: string[];
  expectedSdrSharpCount: number | null;
  expectedSoftEotCount: number | null;
  expectedSoftDpuCount: number | null;
  programs: ServerProgram[];
  diagnostics: ServerDiagnostics | null;
}

export interface UpdateServerExpectations {
  expectedSdrSharpCount: number | null;
  expectedSoftEotCount: number | null;
  expectedSoftDpuCount: number | null;
}
