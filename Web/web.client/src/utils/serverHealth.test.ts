import { describe, expect, it } from 'vitest';
import type { ServerHealth, ServerProgram, ServerStatusChange } from '../types/ServerHealth';
import {
  countByStatus,
  describeMute,
  describeStatusChange,
  filterServers,
  formatGigabytes,
  formatProgramCount,
  formatTaskResult,
  formatTimeAgo,
  formatUptime,
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
    statusSince: null,
    alertsMuted: false,
    alertsMutedUntil: null,
    alertsMuteNote: null,
    alertsMutedBy: null,
    programs: [],
    diagnostics: null,
  };
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
