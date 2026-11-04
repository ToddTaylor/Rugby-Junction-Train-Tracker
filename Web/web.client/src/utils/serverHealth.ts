import type { ServerHealth, ServerProgram, ServerStatus, ServerStatusChange } from '../types/ServerHealth';

export type ServerHealthFilter = 'all' | 'problems' | ServerStatus;

const STATUS_ORDER: Record<ServerStatus, number> = { Offline: 0, Degraded: 1, Online: 2 };

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

/** Offline servers first, then Degraded, then Online; by name within each group. */
export function sortServers(servers: ServerHealth[]): ServerHealth[] {
  return [...servers].sort((a, b) =>
    STATUS_ORDER[a.status] - STATUS_ORDER[b.status] ||
    a.beaconName.localeCompare(b.beaconName, undefined, { sensitivity: 'base' }));
}

export function filterServers(servers: ServerHealth[], filter: ServerHealthFilter): ServerHealth[] {
  if (filter === 'all') return servers;
  if (filter === 'problems') return servers.filter(s => s.status !== 'Online');
  return servers.filter(s => s.status === filter);
}

export function countByStatus(servers: ServerHealth[]): Record<ServerStatus, number> {
  const counts: Record<ServerStatus, number> = { Online: 0, Degraded: 0, Offline: 0 };
  for (const server of servers) {
    counts[server.status] += 1;
  }
  return counts;
}

export function formatTimeAgo(iso: string | null, now: Date = new Date()): string {
  if (!iso) return 'never';

  const elapsed = now.getTime() - new Date(iso).getTime();
  if (elapsed < MINUTE) return 'just now';
  if (elapsed < HOUR) return `${Math.floor(elapsed / MINUTE)} min ago`;
  if (elapsed < 2 * DAY) return `${Math.floor(elapsed / HOUR)} h ago`;
  return `${Math.floor(elapsed / DAY)} days ago`;
}

export function formatUptime(lastBootAt: string | null, now: Date = new Date()): string {
  if (!lastBootAt) return 'unknown';

  const elapsed = Math.max(0, now.getTime() - new Date(lastBootAt).getTime());
  const days = Math.floor(elapsed / DAY);
  const hours = Math.floor((elapsed % DAY) / HOUR);
  const minutes = Math.floor((elapsed % HOUR) / MINUTE);

  if (days > 0) return `${days} d ${hours} h`;
  if (hours > 0) return `${hours} h ${minutes} min`;
  return `${minutes} min`;
}

export function formatGigabytes(bytes: number | null): string {
  if (bytes === null) return 'unknown';
  const gigabytes = bytes / (1024 * 1024 * 1024);
  return `${gigabytes < 10 ? gigabytes.toFixed(1) : Math.round(gigabytes)} GB`;
}

export function formatTaskResult(code: number | null): string {
  return code === null ? '—' : `0x${code.toString(16).toUpperCase()}`;
}

/** "1/2" when an expected count is set, the running count alone when it is not, and "—" without diagnostics. */
export function formatProgramCount(program: ServerProgram): string {
  if (program.running === null) return '—';
  return program.expected === null ? String(program.running) : `${program.running}/${program.expected}`;
}

export function isProgramShort(program: ServerProgram): boolean {
  return program.running !== null && program.expected !== null && program.running < program.expected;
}

/** Parses an expected-count form field: blank means "not checked". Returns undefined when the value is invalid. */
export function parseExpectedCount(value: string, max = 20): number | null | undefined {
  const trimmed = value.trim();
  if (trimmed === '') return null;
  if (!/^\d+$/.test(trimmed)) return undefined;
  const parsed = Number.parseInt(trimmed, 10);
  return parsed <= max ? parsed : undefined;
}

/** Window event the alert listener raises for every ServerStatusChanged, so an open dashboard can refresh. */
export const SERVER_STATUS_CHANGED_EVENT = 'rjtt:server-status-changed';

export type MuteDuration = 'hour' | 'day' | 'week' | 'untilTurnedOn';

export const MUTE_DURATIONS: { value: MuteDuration; label: string }[] = [
  { value: 'hour', label: '1 hour' },
  { value: 'day', label: '1 day' },
  { value: 'week', label: '1 week' },
  { value: 'untilTurnedOn', label: 'Until I turn alerts back on' },
];

/** UTC ISO end time for a mute, or null for a mute with no end. */
export function muteUntilFor(duration: MuteDuration, now: Date = new Date()): string | null {
  const length = { hour: HOUR, day: DAY, week: 7 * DAY, untilTurnedOn: null }[duration];
  return length === null ? null : new Date(now.getTime() + length).toISOString();
}

export function describeMute(server: ServerHealth): string {
  const until = server.alertsMutedUntil
    ? `until ${new Date(server.alertsMutedUntil).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' })}`
    : 'until turned back on';
  return `Alerts muted ${until}${server.alertsMutedBy ? ` by ${server.alertsMutedBy}` : ''}`;
}

/** Title and body for a status-change pop-up or desktop notification. */
export function describeStatusChange(change: ServerStatusChange): { title: string; body: string } {
  if (change.status === 'Online') {
    return {
      title: `${change.beaconName} is back online`,
      body: change.previousStatus === 'Offline' ? 'Checking in again.' : 'The earlier problems have cleared.',
    };
  }

  return {
    title: `${change.beaconName} is ${change.status.toLowerCase()}`,
    body: change.reasons.length > 0 ? change.reasons.join(' · ') : `Status changed from ${change.previousStatus}.`,
  };
}
