import { openRailwaysDB } from './db';
import { fetchApi } from './client';
import type { Beacon } from '../types/Beacon';
import {
  BEACON_SERVER_STATUS_MAP_KEY,
  BEACON_STALE_MAP_KEY,
  BEACON_STATUS_MAP_KEY,
  beaconStatusKey,
  readBeaconMap,
  type StoredServerStatus
} from '../utils/beaconStatusKeys';

const STORE_NAME = 'beacons';
const BEACONS_KEY = 'beacons';
const CACHE_VERSION_KEY = 'beacons_version';

/**
 * Version of the cached beacon shape. Increase it when the shape changes, so a browser holding an older
 * cache shows nothing until the server answers instead of rows with missing fields.
 */
export const BEACON_CACHE_VERSION = 6; // v6: Server Health status replaces the offline note (issue #96)

/** A BeaconRailroads row as the server sends it, in REST responses and in the BeaconUpdate message. */
export interface BeaconRailroadDto {
  beaconID: string;
  beaconName: string;
  railroadID: string;
  subdivisionID: string;
  railroadName?: string;
  subdivisionName?: string;
  latitude: number;
  longitude: number;
  milepost: number;
  online: boolean;
  telemetryStale?: boolean;
  serverStatus?: Beacon['serverStatus'];
  serverStatusReasons?: string[];
}

/** Maps the server's field names (railroadName, subdivisionName) to the Beacon type's (railroad, subdivision). */
export function toBeacon(dto: BeaconRailroadDto): Beacon {
  return {
    beaconID: dto.beaconID,
    beaconName: dto.beaconName,
    railroadID: dto.railroadID,
    subdivisionID: dto.subdivisionID,
    railroad: dto.railroadName,
    subdivision: dto.subdivisionName,
    latitude: dto.latitude,
    longitude: dto.longitude,
    milepost: dto.milepost,
    online: dto.online,
    telemetryStale: dto.telemetryStale,
    serverStatus: dto.serverStatus,
    serverStatusReasons: dto.serverStatusReasons ?? []
  };
}

/**
 * Lays the statuses last pushed over SignalR, which useBeacons keeps in localStorage, over a beacon list.
 *
 * The REST endpoint does not compute telemetryStale and its other statuses can be a minute behind, so the
 * stored values are the better ones for a beacon that has them. A beacon with nothing stored keeps what the
 * list says. For a few seconds after the tab regains focus (focusGraceUntil), a beacon stored as online is
 * not turned offline, which avoids a flicker before the next BeaconUpdate.
 */
export function applyStoredStatus(beacons: Beacon[], now: number = Date.now()): Beacon[] {
  const statusMap = readBeaconMap<boolean>(BEACON_STATUS_MAP_KEY);
  const telemetryStaleMap = readBeaconMap<boolean>(BEACON_STALE_MAP_KEY);
  const serverStatusMap = readBeaconMap<StoredServerStatus>(BEACON_SERVER_STATUS_MAP_KEY);
  const graceUntil = Number(localStorage.getItem('focusGraceUntil') || '0');

  return beacons.map(beacon => {
    const key = beaconStatusKey(beacon.beaconID, beacon.subdivisionID);
    const storedOnline = statusMap[key];
    const storedStale = telemetryStaleMap[key];
    const storedServerStatus = serverStatusMap[key];

    let result = beacon;
    if (storedOnline === true && beacon.online === false && now < graceUntil) {
      result = { ...result, online: true };
    } else if (storedOnline !== undefined) {
      result = { ...result, online: storedOnline };
    }
    if (storedStale !== undefined) {
      result = { ...result, telemetryStale: storedStale };
    }
    if (storedServerStatus !== undefined) {
      result = { ...result, ...storedServerStatus };
    }
    return result;
  });
}

/**
 * The beacon list saved by the last successful fetch, or null when there is none, it is from an older
 * version, or the browser's database cannot be read.
 */
export async function readCachedBeacons(): Promise<Beacon[] | null> {
  try {
    const db = await openRailwaysDB();
    const version = await db.get(STORE_NAME, CACHE_VERSION_KEY);
    if (version !== BEACON_CACHE_VERSION) {
      return null;
    }

    const cached = await db.get(STORE_NAME, BEACONS_KEY);
    return Array.isArray(cached) ? (cached as Beacon[]) : null;
  } catch (error) {
    console.warn('Could not read cached beacons:', error);
    return null;
  }
}

async function writeCachedBeacons(beacons: Beacon[]): Promise<void> {
  try {
    const db = await openRailwaysDB();
    await db.put(STORE_NAME, beacons, BEACONS_KEY);
    await db.put(STORE_NAME, BEACON_CACHE_VERSION, CACHE_VERSION_KEY);
  } catch (error) {
    // The cache only speeds up the next page load, so a full or blocked database is not an error for the caller.
    console.warn('Could not cache beacons:', error);
  }
}

/**
 * Fetches the beacon list from the server and saves it for the next page load. Throws when the server
 * cannot be reached or refuses, so the caller can tell a refresh that worked from one that did not.
 */
export async function fetchBeaconsFromServer(): Promise<Beacon[]> {
  const response = await fetchApi<BeaconRailroadDto[]>('/api/v1/BeaconRailroads');
  if (response.errors.length > 0 || !Array.isArray(response.data)) {
    throw new Error(`Failed to fetch beacons: ${response.errors.join(', ') || 'no data'}`);
  }

  const beacons = response.data.map(toBeacon);
  await writeCachedBeacons(beacons);
  return beacons;
}

/**
 * Loads the beacons for the map: the cached list first, so the map draws at once, then always the server's
 * list, so beacons an admin added, moved or renamed appear without waiting for a new release.
 *
 * onBeacons is called once per list that arrives, with stored statuses applied. If the server cannot be
 * reached the cached list stays; the promise rejects only when there is no list to show at all.
 */
export async function loadBeacons(onBeacons: (beacons: Beacon[]) => void): Promise<void> {
  const cached = await readCachedBeacons();
  if (cached) {
    onBeacons(applyStoredStatus(cached));
  }

  try {
    onBeacons(applyStoredStatus(await fetchBeaconsFromServer()));
  } catch (error) {
    if (!cached) {
      throw error;
    }
    console.warn('Showing cached beacons; the server could not be reached:', error);
  }
}
