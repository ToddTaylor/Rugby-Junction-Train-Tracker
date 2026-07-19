import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fetchBeacons } from './beaconsApi';
import { openRailwaysDB } from './db';
import { fetchWithAuth } from '../utils/fetchWithAuth';

vi.mock('./db', () => ({
  openRailwaysDB: vi.fn()
}));

vi.mock('../utils/fetchWithAuth', () => ({
  fetchWithAuth: vi.fn()
}));

const mockedOpenRailwaysDB = vi.mocked(openRailwaysDB);
const mockedFetchWithAuth = vi.mocked(fetchWithAuth);

function makeFakeDb(cached: any) {
  return {
    get: vi.fn().mockResolvedValue(cached),
    put: vi.fn().mockResolvedValue(undefined),
    close: vi.fn().mockResolvedValue(undefined)
  };
}

describe('fetchBeacons (soft-refresh / visibility-change path)', () => {
  beforeEach(() => {
    localStorage.clear();
    mockedOpenRailwaysDB.mockReset();
    mockedFetchWithAuth.mockReset();
  });

  it('reproduces the bug scenario: preserves a SignalR-confirmed telemetryStale=true instead of reverting it to the cached false', async () => {
    // Simulates the exact production failure: IndexedDB cache holds a snapshot
    // from the REST endpoint (which never computes staleness, so telemetryStale
    // is always false there), while localStorage holds the freshest truth that
    // SignalR pushed a minute ago (beacon 42 has gone stale).
    const cachedBeacons = [
      { beaconID: '42', beaconName: 'Owen', online: true, telemetryStale: false }
    ];
    mockedOpenRailwaysDB.mockResolvedValue(makeFakeDb(cachedBeacons) as any);
    localStorage.setItem('beaconStatusMap', JSON.stringify({ '42': true }));
    localStorage.setItem('beaconTelemetryStaleMap', JSON.stringify({ '42': true }));

    const setBeacons = vi.fn();
    const setBeaconsLoaded = vi.fn();

    await fetchBeacons(setBeacons, setBeaconsLoaded);

    expect(setBeacons).toHaveBeenCalledTimes(1);
    const result = setBeacons.mock.calls[0][0];
    expect(result).toEqual([
      { beaconID: '42', beaconName: 'Owen', online: true, telemetryStale: true }
    ]);
    expect(setBeaconsLoaded).toHaveBeenCalledWith(true);
    // The bare REST endpoint should never be hit once a cache entry exists.
    expect(mockedFetchWithAuth).not.toHaveBeenCalled();
  });

  it('leaves telemetryStale as-is when nothing has been recorded for that beacon yet', async () => {
    const cachedBeacons = [
      { beaconID: '99', beaconName: 'NewBeacon', online: true, telemetryStale: false }
    ];
    mockedOpenRailwaysDB.mockResolvedValue(makeFakeDb(cachedBeacons) as any);
    // No beaconTelemetryStaleMap entry for beacon 99.

    const setBeacons = vi.fn();
    const setBeaconsLoaded = vi.fn();

    await fetchBeacons(setBeacons, setBeaconsLoaded);

    const result = setBeacons.mock.calls[0][0];
    expect(result[0].telemetryStale).toBe(false);
  });

  it('still applies the existing online grace-period overlay alongside the new telemetryStale preservation', async () => {
    const cachedBeacons = [
      { beaconID: '7', beaconName: 'Flicker', online: false, telemetryStale: true }
    ];
    mockedOpenRailwaysDB.mockResolvedValue(makeFakeDb(cachedBeacons) as any);
    localStorage.setItem('beaconStatusMap', JSON.stringify({ '7': true }));
    localStorage.setItem('focusGraceUntil', String(Date.now() + 5000));

    const setBeacons = vi.fn();
    const setBeaconsLoaded = vi.fn();

    await fetchBeacons(setBeacons, setBeaconsLoaded);

    const result = setBeacons.mock.calls[0][0];
    expect(result[0].online).toBe(true); // preserved via grace period, unchanged behavior
    expect(result[0].telemetryStale).toBe(true); // untouched, no stored override present
  });
});
