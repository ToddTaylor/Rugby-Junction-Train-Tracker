import { describe, expect, it } from 'vitest';
import type { BeaconCommand, ServerDiagnostics, ServerHealth, ServerProgram, ServerStatusChange } from '../types/ServerHealth';
import {
  activeCommand,
  commandAvailability,
  commandStatusLabel,
  countByStatus,
  describeCommandUpdate,
  describeMute,
  describeStatusChange,
  filterServers,
  formatGigabytes,
  formatProgramCount,
  formatTaskResult,
  formatTimeAgo,
  formatUptime,
  isProgramInSummary,
  isProgramShort,
  muteUntilFor,
  parseExpectedCount,
  sortServers,
} from './serverHealth';

const NOW = new Date('2026-11-03T12:00:00Z');

function server(beaconName: string, status: ServerHealth['status']): ServerHealth {
  return {
    beaconID: beaconName.length,
    beaconName,
    status,
    reasons: [],
    lastCheckIn: null,
    offlineThresholdMinutes: 6,
    hasDiagnostics: true,
    subdivisions: [],
    expectedSdrSharpCount: null,
    expectedSoftEotCount: null,
    expectedSoftDpuCount: null,
    expectedSdrSliceCount: null,
    statusSince: null,
    alertsMuted: false,
    alertsMutedUntil: null,
    alertsMuteNote: null,
    alertsMutedBy: null,
    deviceKeySetAt: null,
    deviceKeySetBy: null,
    deviceKeyRejectedAt: null,
    rebootsLast24Hours: 0,
    maxRebootsPerDay: 3,
    commands: [],
    programs: [],
    diagnostics: null,
  };
}

function diagnostics(remoteCommandsEnabled: boolean, restartAppsAvailable = false): ServerDiagnostics {
  return {
    receivedAt: NOW.toISOString(),
    collectedAt: NOW.toISOString(),
    hostname: 'SUSSEX-NUC',
    clientVersion: '2.5.0.0',
    osVersion: null,
    lastBootAt: null,
    logDriveFreeBytes: null,
    healthPingIntervalMinutes: 2,
    lastTelemetrySentAt: null,
    telemetrySendFailuresLastHour: 0,
    remoteCommandsEnabled,
    restartAppsAvailable,
    logs: [],
    startupTasks: [],
  };
}

function command(status: BeaconCommand['status'], type: BeaconCommand['type'] = 'Reboot', extra: Partial<BeaconCommand> = {}): BeaconCommand {
  return {
    id: 7,
    type,
    status,
    requestedBy: 'Ada Admin',
    requestedAt: NOW.toISOString(),
    expiresAt: new Date(NOW.getTime() + 600_000).toISOString(),
    deliveredAt: null,
    startedAt: null,
    finishedAt: null,
    resultMessage: null,
    cancelledBy: null,
    ...extra,
  };
}

/** An Online server with a device key, reporting that it allows remote commands. */
function readyServer(): ServerHealth {
  return { ...server('Sussex', 'Online'), deviceKeySetAt: NOW.toISOString(), diagnostics: diagnostics(true, true) };
}

function program(running: number | null, expected: number | null): ServerProgram {
  return { name: 'SoftEOT', running, expected, notResponding: 0 };
}

describe('sortServers', () => {
  it('puts Offline first, then Degraded, then Online, by name within each', () => {
    const sorted = sortServers([
      server('Waukesha', 'Online'),
      server('Sussex', 'Offline'),
      server('Slinger', 'Degraded'),
      server('Duplainville', 'Online'),
      server('rugby Junction', 'Degraded'),
    ]);

    expect(sorted.map(s => s.beaconName)).toEqual(['Sussex', 'rugby Junction', 'Slinger', 'Duplainville', 'Waukesha']);
  });
});

describe('filterServers and countByStatus', () => {
  const servers = [server('A', 'Online'), server('B', 'Degraded'), server('C', 'Offline'), server('D', 'Online')];

  it('filters to problems or one status', () => {
    expect(filterServers(servers, 'all')).toHaveLength(4);
    expect(filterServers(servers, 'problems').map(s => s.beaconName)).toEqual(['B', 'C']);
    expect(filterServers(servers, 'Online').map(s => s.beaconName)).toEqual(['A', 'D']);
  });

  it('counts each status', () => {
    expect(countByStatus(servers)).toEqual({ Online: 2, Degraded: 1, Offline: 1 });
  });
});

describe('formatting', () => {
  it('formats time since a check-in', () => {
    expect(formatTimeAgo(null, NOW)).toBe('never');
    expect(formatTimeAgo('2026-11-03T11:59:30Z', NOW)).toBe('just now');
    expect(formatTimeAgo('2026-11-03T11:13:00Z', NOW)).toBe('47 min ago');
    expect(formatTimeAgo('2026-11-03T04:00:00Z', NOW)).toBe('8 h ago');
    expect(formatTimeAgo('2026-10-25T12:00:00Z', NOW)).toBe('9 days ago');
  });

  it('formats uptime from the last boot', () => {
    expect(formatUptime('2026-10-31T08:00:00Z', NOW)).toBe('3 d 4 h');
    expect(formatUptime('2026-11-03T06:50:00Z', NOW)).toBe('5 h 10 min');
    expect(formatUptime('2026-11-03T11:52:00Z', NOW)).toBe('8 min');
    expect(formatUptime(null, NOW)).toBe('unknown');
  });

  it('formats disk space and task results', () => {
    expect(formatGigabytes(41 * 1024 ** 3)).toBe('41 GB');
    expect(formatGigabytes(1.25 * 1024 ** 3)).toBe('1.3 GB');
    expect(formatGigabytes(null)).toBe('unknown');
    expect(formatTaskResult(0)).toBe('0x0');
    expect(formatTaskResult(267011)).toBe('0x41303');
    expect(formatTaskResult(null)).toBe('—');
  });
});

describe('program counts', () => {
  it('shows running/expected, running alone, or a dash', () => {
    expect(formatProgramCount(program(1, 2))).toBe('1/2');
    expect(formatProgramCount(program(3, null))).toBe('3');
    expect(formatProgramCount(program(null, 2))).toBe('—');
  });

  it('flags a program running fewer instances than expected', () => {
    expect(isProgramShort(program(1, 2))).toBe(true);
    expect(isProgramShort(program(2, 2))).toBe(false);
    expect(isProgramShort(program(0, null))).toBe(false);
    expect(isProgramShort(program(null, 2))).toBe(false);
  });

  it('leaves unchecked programs with nothing running out of the summary', () => {
    expect(isProgramInSummary(program(0, null))).toBe(false);
    expect(isProgramInSummary(program(6, null))).toBe(true);
    expect(isProgramInSummary(program(0, 6))).toBe(true);
    expect(isProgramInSummary(program(null, null))).toBe(false);
    expect(isProgramInSummary(program(null, 6))).toBe(true);
  });
});

describe('parseExpectedCount', () => {
  it('treats blank as not checked and rejects invalid values', () => {
    expect(parseExpectedCount('')).toBeNull();
    expect(parseExpectedCount('  ')).toBeNull();
    expect(parseExpectedCount('4')).toBe(4);
    expect(parseExpectedCount('0')).toBe(0);
    expect(parseExpectedCount('-1')).toBeUndefined();
    expect(parseExpectedCount('2.5')).toBeUndefined();
    expect(parseExpectedCount('21')).toBeUndefined();
  });
});

describe('muteUntilFor', () => {
  it('adds the chosen length to now', () => {
    expect(muteUntilFor('hour', NOW)).toBe('2026-11-03T13:00:00.000Z');
    expect(muteUntilFor('week', NOW)).toBe('2026-11-10T12:00:00.000Z');
  });

  it('has no end time for a mute that lasts until alerts are turned back on', () => {
    expect(muteUntilFor('untilTurnedOn', NOW)).toBeNull();
  });
});

describe('describeMute', () => {
  it('names who muted an open-ended mute', () => {
    const muted = { ...server('Sussex', 'Offline'), alertsMuted: true, alertsMutedBy: 'Todd Taylor' };
    expect(describeMute(muted)).toBe('Alerts muted until turned back on by Todd Taylor');
  });

  it('shows when a timed mute ends', () => {
    const muted = { ...server('Sussex', 'Offline'), alertsMuted: true, alertsMutedUntil: '2026-11-04T12:00:00Z' };
    expect(describeMute(muted)).toMatch(/^Alerts muted until .+2026/);
  });
});

describe('describeStatusChange', () => {
  const change = (status: ServerStatusChange['status'], previousStatus: ServerStatusChange['status'], reasons: string[] = []): ServerStatusChange => ({
    beaconID: 1,
    beaconName: 'Sussex',
    previousStatus,
    status,
    reasons,
    changedAt: NOW.toISOString(),
    alertsMuted: false,
  });

  it('lists the reasons for a problem', () => {
    expect(describeStatusChange(change('Degraded', 'Online', ['SDR# is not running', 'Low disk space']))).toEqual({
      title: 'Sussex is degraded',
      body: 'SDR# is not running · Low disk space',
    });
  });

  it('says a server that was offline is checking in again', () => {
    expect(describeStatusChange(change('Online', 'Offline'))).toEqual({
      title: 'Sussex is back online',
      body: 'Checking in again.',
    });
  });

  it('says a degraded server has cleared', () => {
    expect(describeStatusChange(change('Online', 'Degraded')).body).toBe('The earlier problems have cleared.');
  });
});

describe('commandAvailability', () => {
  it('allows a reboot on a ready server', () => {
    expect(commandAvailability(readyServer(), 'Reboot')).toEqual({ allowed: true });
    expect(commandAvailability(readyServer(), 'RestartApps')).toEqual({ allowed: true });
  });

  it('needs a device key first', () => {
    const result = commandAvailability({ ...readyServer(), deviceKeySetAt: null }, 'Reboot');
    expect(result.allowed).toBe(false);
    expect(result.reason).toContain('device key');
  });

  it('refuses an offline server', () => {
    expect(commandAvailability({ ...readyServer(), status: 'Offline' }, 'Reboot').reason).toContain('offline');
  });

  it('refuses a PC that does not allow remote commands, or has no diagnostics', () => {
    expect(commandAvailability({ ...readyServer(), diagnostics: diagnostics(false) }, 'Reboot').allowed).toBe(false);
    expect(commandAvailability({ ...readyServer(), diagnostics: null }, 'Reboot').allowed).toBe(false);
  });

  it('offers restart apps only when the PC has the command set up', () => {
    const server = { ...readyServer(), diagnostics: diagnostics(true, false) };
    expect(commandAvailability(server, 'RestartApps').allowed).toBe(false);
    expect(commandAvailability(server, 'Reboot').allowed).toBe(true);
  });

  it('allows one command at a time', () => {
    const server = { ...readyServer(), commands: [command('Pending')] };
    expect(commandAvailability(server, 'Reboot').reason).toContain('already');
    expect(commandAvailability(server, 'RestartApps').allowed).toBe(false);
  });

  it('stops at the daily reboot limit but still allows restarting the apps', () => {
    const server = { ...readyServer(), rebootsLast24Hours: 3 };
    expect(commandAvailability(server, 'Reboot').allowed).toBe(false);
    expect(commandAvailability(server, 'RestartApps').allowed).toBe(true);
  });
});

describe('activeCommand', () => {
  it('finds a waiting or running command and ignores finished ones', () => {
    expect(activeCommand({ ...readyServer(), commands: [command('Succeeded'), command('Expired')] })).toBeNull();
    expect(activeCommand({ ...readyServer(), commands: [command('Running', 'Reboot', { id: 9 }), command('Failed')] })?.id).toBe(9);
  });
});

describe('commandStatusLabel', () => {
  it('tells a waiting command apart from one the PC has received', () => {
    expect(commandStatusLabel(command('Pending'))).toBe('Waiting for the PC');
    expect(commandStatusLabel(command('Pending', 'Reboot', { deliveredAt: NOW.toISOString() }))).toBe('Sent to the PC');
    expect(commandStatusLabel(command('Cancelled', 'Reboot', { cancelledBy: 'Cody Custodian' }))).toBe('Cancelled by Cody Custodian');
    expect(commandStatusLabel(command('Running', 'RestartApps'))).toBe('Restarting apps');
  });
});

describe('describeCommandUpdate', () => {
  it('announces a request with who asked', () => {
    const text = describeCommandUpdate({ beaconID: 1, beaconName: 'Sussex', command: command('Pending') });
    expect(text.title).toBe('Reboot of Sussex requested');
    expect(text.body).toContain('Ada Admin');
    expect(text.tone).toBe('info');
  });

  it('shows the PC reason for a refusal as a warning', () => {
    const text = describeCommandUpdate({
      beaconID: 1,
      beaconName: 'Sussex',
      command: command('Rejected', 'Reboot', { resultMessage: 'The PC started 3 min ago.' }),
    });
    expect(text.title).toBe('Sussex refused the reboot');
    expect(text.body).toBe('The PC started 3 min ago.');
    expect(text.tone).toBe('degraded');
  });

  it('marks a failure as an error and a success as online', () => {
    expect(describeCommandUpdate({ beaconID: 1, beaconName: 'Sussex', command: command('Failed', 'RestartApps') }).tone).toBe('offline');
    expect(describeCommandUpdate({ beaconID: 1, beaconName: 'Sussex', command: command('Succeeded') }).title).toBe('Sussex restarted');
  });
});
