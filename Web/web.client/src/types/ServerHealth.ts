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
  /** The PC allows remote commands (AllowRemoteCommands) and has its device key entered. */
  remoteCommandsEnabled: boolean;
  /** The PC has a RestartAppsCommand set up. */
  restartAppsAvailable: boolean;
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
  /** SDR# slice windows (Sharp Slicer) that should be open. Null for PCs that do not run SDR# in slice mode. */
  expectedSdrSliceCount: number | null;
  /** When the server entered its current status. Null until the alert monitor has seen it. */
  statusSince: string | null;
  alertsMuted: boolean;
  /** Null while muted means muted until someone turns alerts back on. */
  alertsMutedUntil: string | null;
  alertsMuteNote: string | null;
  alertsMutedBy: string | null;
  /** When the device key was created. Null when the server has none, so it cannot be rebooted remotely. */
  deviceKeySetAt: string | null;
  deviceKeySetBy: string | null;
  /** Last check-in refused because the device key was missing or wrong. */
  deviceKeyRejectedAt: string | null;
  rebootsLast24Hours: number;
  maxRebootsPerDay: number;
  /** Recent reboot and restart-apps commands, newest first. */
  commands: BeaconCommand[];
  programs: ServerProgram[];
  diagnostics: ServerDiagnostics | null;
}

export interface UpdateServerExpectations {
  expectedSdrSharpCount: number | null;
  expectedSoftEotCount: number | null;
  expectedSoftDpuCount: number | null;
  /** SDR# slice windows (Sharp Slicer) that should be open. Null for PCs that do not run SDR# in slice mode. */
  expectedSdrSliceCount: number | null;
}

export interface MuteServerAlerts {
  /** UTC ISO time when alerts turn back on, or null to mute until turned back on. */
  mutedUntil: string | null;
  note: string | null;
}

/** SignalR ServerStatusChanged payload. */
export interface ServerStatusChange {
  beaconID: number;
  beaconName: string;
  previousStatus: ServerStatus;
  status: ServerStatus;
  reasons: string[];
  changedAt: string;
  /** True when alerts are muted: refresh the dashboard, but show no pop-up. */
  alertsMuted: boolean;
}

export type BeaconCommandType = 'Reboot' | 'RestartApps';

export type BeaconCommandStatus = 'Pending' | 'Running' | 'Succeeded' | 'Failed' | 'Rejected' | 'Cancelled' | 'Expired';

export interface BeaconCommand {
  id: number;
  type: BeaconCommandType;
  status: BeaconCommandStatus;
  requestedBy: string;
  requestedAt: string;
  expiresAt: string;
  deliveredAt: string | null;
  startedAt: string | null;
  finishedAt: string | null;
  resultMessage: string | null;
  cancelledBy: string | null;
}

/** SignalR ServerCommandUpdated payload. */
export interface ServerCommandUpdate {
  beaconID: number;
  beaconName: string;
  command: BeaconCommand;
}
