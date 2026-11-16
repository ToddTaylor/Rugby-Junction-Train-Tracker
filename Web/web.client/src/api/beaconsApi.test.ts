import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  BEACON_CACHE_VERSION,
  applyStoredStatus,
  fetchBeaconsFromServer,
  loadBeacons,
  toBeacon,
  type BeaconRailroadDto
} from './beaconsApi';
import { openRailwaysDB } from './db';
import { fetchApi } from './client';
import type { Beacon } from '../types/Beacon';

vi.mock('./db', () => ({
  openRailwaysDB: vi.fn()
}));

vi.mock('./client', () => ({
  fetchApi: vi.fn()
}));

const mockedOpenRailwaysDB = vi.mocked(openRailwaysDB);
const mockedFetchApi = vi.mocked(fetchApi);

/** An in-memory stand-in for the browser database, holding whatever the test seeds it with. */
function makeFakeDb(seed: Record<string, unknown> = {}) {
  const values = new Map<string, unknown>(Object.entries(seed));
  return {
    values,
    get: vi.fn(async (_store: string, key: string) => values.get(key)),
    put: vi.fn(async (_store: string, value: unknown, key: string) => { values.set(key, value); })
  };
}

function useDb(seed: Record<string, unknown> = {}) {
  const db = makeFakeDb(seed);
  mockedOpenRailwaysDB.mockResolvedValue(db as never);
  return db;
}

/** Seeds the database with a beacon list saved by the current version of the app. */
function useCachedBeacons(beacons: Beacon[]) {
  return useDb({ beacons, beacons_version: BEACON_CACHE_VERSION });
}

function serverReturns(rows: BeaconRailroadDto[]) {
  mockedFetchApi.mockResolvedValue({ data: rows, errors: [] });
}

function serverIsDown() {
  mockedFetchApi.mockResolvedValue({ data: null, errors: ['Failed to fetch'] });
}

function row(overrides: Partial<BeaconRailroadDto> = {}): BeaconRailroadDto {
  return {
    beaconID: '42',
    beaconName: 'Owen',
    railroadID: '1',
    subdivisionID: '3',
    railroadName: 'CN',
    subdivisionName: 'Superior',
    latitude: 44.9,
    longitude: -90.5,
    milepost: 300.1,
    online: true,
    telemetryStale: false,
    serverStatus: 'Online',
    serverStatusReasons: [],
    ...overrides
  };
}

function beacon(overrides: Partial<BeaconRailroadDto> = {}): Beacon {
  return toBeacon(row(overrides));
}

describe('toBeacon', () => {
  it("maps the server's railroadName and subdivisionName to railroad and subdivision", () => {
    const result = toBeacon(row());

    expect(result.railroad).toBe('CN');
    expect(result.subdivision).toBe('Superior');
    expect(result).not.toHaveProperty('railroadName');
  });

  it('gives a row without reasons an empty list', () => {
    expect(toBeacon(row({ serverStatusReasons: undefined })).serverStatusReasons).toEqual([]);
  });
});

describe('applyStoredStatus', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('keeps a telemetryStale=true pushed over SignalR instead of the false the REST endpoint always sends', () => {
    // The REST endpoint never computes staleness, so its telemetryStale is always false. localStorage
    // holds what SignalR pushed last, which is the truth.
    localStorage.setItem('beaconStatusMapV2', JSON.stringify({ '42|3': true }));
    localStorage.setItem('beaconTelemetryStaleMapV2', JSON.stringify({ '42|3': true }));

    const [result] = applyStoredStatus([beacon({ telemetryStale: false })]);

    expect(result.online).toBe(true);
    expect(result.telemetryStale).toBe(true);
  });

  it('keeps the stored Server Health status and reasons', () => {
    localStorage.setItem('beaconStatusMapV2', JSON.stringify({ '42|3': false }));
    localStorage.setItem('beaconServerStatusMapV1', JSON.stringify({
      '42|3': { serverStatus: 'Degraded', serverStatusReasons: ['SoftEOT not running'] }
    }));

    const [result] = applyStoredStatus([beacon()]);

    expect(result.online).toBe(false);
    expect(result.serverStatus).toBe('Degraded');
    expect(result.serverStatusReasons).toEqual(['SoftEOT not running']);
  });

  it('leaves a beacon with nothing stored as the list has it', () => {
    const original = beacon({ online: false, serverStatus: 'Offline', serverStatusReasons: ['Never checked in'] });

    const [result] = applyStoredStatus([original]);

    expect(result).toEqual(original);
  });

  it('keys stored statuses by beacon and subdivision, so a junction beacon keeps one status per subdivision', () => {
    localStorage.setItem('beaconTelemetryStaleMapV2', JSON.stringify({ '42|3': true, '42|4': false }));

    const result = applyStoredStatus([
      beacon({ subdivisionID: '3', telemetryStale: false }),
      beacon({ subdivisionID: '4', telemetryStale: true })
    ]);

    expect(result.map(b => b.telemetryStale)).toEqual([true, false]);
  });

  it('does not turn a beacon offline during the grace period after the tab regains focus', () => {
    localStorage.setItem('beaconStatusMapV2', JSON.stringify({ '42|3': true }));
    localStorage.setItem('focusGraceUntil', '5000');

    const [duringGrace] = applyStoredStatus([beacon({ online: false })], 4000);

    expect(duringGrace.online).toBe(true);
  });
});

describe('fetchBeaconsFromServer', () => {
  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    localStorage.clear();
    mockedOpenRailwaysDB.mockReset();
    mockedFetchApi.mockReset();
  });

  it('returns mapped beacons and saves them with the cache version', async () => {
    const db = useDb();
    serverReturns([row()]);

    const result = await fetchBeaconsFromServer();

    expect(mockedFetchApi).toHaveBeenCalledWith('/api/v1/BeaconRailroads');
    expect(result).toEqual([beacon()]);
    expect(db.values.get('beacons')).toEqual([beacon()]);
    expect(db.values.get('beacons_version')).toBe(BEACON_CACHE_VERSION);
  });

  it('throws when the server cannot be reached, and leaves the cache alone', async () => {
    const db = useCachedBeacons([beacon()]);
    serverIsDown();

    await expect(fetchBeaconsFromServer()).rejects.toThrow('Failed to fetch beacons');
    expect(db.put).not.toHaveBeenCalled();
  });

  it('still returns the beacons when the browser database cannot be written', async () => {
    mockedOpenRailwaysDB.mockRejectedValue(new Error('database blocked'));
    serverReturns([row()]);

    await expect(fetchBeaconsFromServer()).resolves.toEqual([beacon()]);
  });
});

describe('loadBeacons', () => {
  beforeEach(() => {
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    localStorage.clear();
    mockedOpenRailwaysDB.mockReset();
    mockedFetchApi.mockReset();
  });

  it('shows the cached list first, then always the list from the server', async () => {
    useCachedBeacons([beacon({ beaconName: 'Owen' })]);
    serverReturns([row({ beaconName: 'Owen' }), row({ beaconID: '50', beaconName: 'Added By Admin' })]);
    const onBeacons = vi.fn();

    await loadBeacons(onBeacons);

    expect(onBeacons).toHaveBeenCalledTimes(2);
    expect(onBeacons.mock.calls[0][0].map((b: Beacon) => b.beaconName)).toEqual(['Owen']);
    expect(onBeacons.mock.calls[1][0].map((b: Beacon) => b.beaconName)).toEqual(['Owen', 'Added By Admin']);
  });

  it('picks up a beacon the admin moved or renamed, and drops one that was deleted', async () => {
    useCachedBeacons([beacon({ beaconName: 'Old Name', latitude: 1 }), beacon({ beaconID: '9', beaconName: 'Deleted' })]);
    serverReturns([row({ beaconName: 'New Name', latitude: 2 })]);
    const onBeacons = vi.fn();

    await loadBeacons(onBeacons);

    const latest: Beacon[] = onBeacons.mock.calls.at(-1)![0];
    expect(latest).toHaveLength(1);
    expect(latest[0].beaconName).toBe('New Name');
    expect(latest[0].latitude).toBe(2);
  });

  it('applies stored statuses to the cached list and to the list from the server', async () => {
    useCachedBeacons([beacon()]);
    serverReturns([row()]);
    localStorage.setItem('beaconTelemetryStaleMapV2', JSON.stringify({ '42|3': true }));
    const onBeacons = vi.fn();

    await loadBeacons(onBeacons);

    expect(onBeacons.mock.calls[0][0][0].telemetryStale).toBe(true);
    expect(onBeacons.mock.calls[1][0][0].telemetryStale).toBe(true);
  });

  it('keeps the cached list when the server cannot be reached', async () => {
    useCachedBeacons([beacon()]);
    serverIsDown();
    const onBeacons = vi.fn();

    await expect(loadBeacons(onBeacons)).resolves.toBeUndefined();

    expect(onBeacons).toHaveBeenCalledTimes(1);
    expect(onBeacons.mock.calls[0][0]).toEqual([beacon()]);
  });

  it('rejects when there is no cached list and the server cannot be reached', async () => {
    useDb();
    serverIsDown();
    const onBeacons = vi.fn();

    await expect(loadBeacons(onBeacons)).rejects.toThrow('Failed to fetch beacons');
    expect(onBeacons).not.toHaveBeenCalled();
  });

  it('ignores a cached list saved by an older version of the app', async () => {
    useDb({ beacons: [beacon({ beaconName: 'Old Shape' })], beacons_version: BEACON_CACHE_VERSION - 1 });
    serverReturns([row({ beaconName: 'Current' })]);
    const onBeacons = vi.fn();

    await loadBeacons(onBeacons);

    expect(onBeacons).toHaveBeenCalledTimes(1);
    expect(onBeacons.mock.calls[0][0][0].beaconName).toBe('Current');
  });

  it('loads from the server when the browser database cannot be opened', async () => {
    mockedOpenRailwaysDB.mockRejectedValue(new Error('database blocked'));
    serverReturns([row()]);
    const onBeacons = vi.fn();

    await loadBeacons(onBeacons);

    expect(onBeacons).toHaveBeenCalledTimes(1);
    expect(onBeacons.mock.calls[0][0]).toEqual([beacon()]);
  });
});
