import { openRailwaysDB } from './db';
import { fetchWithAuth } from '../utils/fetchWithAuth';
import {
  BEACON_OFFLINE_NOTE_MAP_KEY,
  BEACON_STALE_MAP_KEY,
  BEACON_STATUS_MAP_KEY,
  beaconStatusKey,
  readBeaconMap
} from '../utils/beaconStatusKeys';

// Fetch and cache beacons with status persistence & focus grace period overlay.
export const fetchBeacons = async (setBeacons: any, setBeaconsLoaded: any) => {
  const STORE_NAME = 'beacons';
  const db = await openRailwaysDB();
  let cached: any;
  try {
    cached = await db.get(STORE_NAME, 'beacons');
  } catch (e) {
    await db.close();
    await indexedDB.deleteDatabase('railways-db');
    const db2 = await openRailwaysDB();
    cached = await db2.get(STORE_NAME, 'beacons');
  }

  const statusMap = readBeaconMap<boolean>(BEACON_STATUS_MAP_KEY);
  const telemetryStaleMap = readBeaconMap<boolean>(BEACON_STALE_MAP_KEY);
  const offlineNoteMap = readBeaconMap<string | null>(BEACON_OFFLINE_NOTE_MAP_KEY);
  const graceUntil = Number(localStorage.getItem('focusGraceUntil') || '0');
  const now = Date.now();

  if (cached) {
    const withStatus = (cached as any[]).map(b => {
      const key = beaconStatusKey(b.beaconID, b.subdivisionID);
      const prevOnline = statusMap[key];
      const prevStale = telemetryStaleMap[key];
      const prevOfflineNote = offlineNoteMap[key];
      let result = b;
      if (prevOnline === true && b.online === false && now < graceUntil) {
        result = { ...result, online: true };
      } else if (prevOnline !== undefined) {
        result = { ...result, online: prevOnline };
      }
      if (prevStale !== undefined) {
        result = { ...result, telemetryStale: prevStale };
      }
      if (prevOfflineNote !== undefined) {
        result = { ...result, offlineNote: prevOfflineNote };
      }
      return result;
    });
    setBeacons(withStatus);
    setBeaconsLoaded(true);
    return;
  }

  try {
    const apiUrl = import.meta.env.VITE_API_URL + '/api/v1/BeaconRailroads';
    const response = await fetchWithAuth(apiUrl, {
      headers: {
        'X-Api-Key': import.meta.env.VITE_API_KEY,
        'Content-Type': 'application/json'
      }
    });
    if (!response.ok) throw new Error('Failed to fetch beacons');
    const { data: beacons } = await response.json();
    await db.put(STORE_NAME, beacons, 'beacons');
    const withStatus = (beacons as any[]).map(b => {
      const prevOnline = statusMap[b.beaconID];
      const prevStale = telemetryStaleMap[b.beaconID];
      const prevOfflineNote = offlineNoteMap[b.beaconID];
      let result = b;
      if (prevOnline === true && b.online === false && now < graceUntil) {
        result = { ...result, online: true };
      } else if (prevOnline !== undefined) {
        result = { ...result, online: prevOnline };
      }
      if (prevStale !== undefined) {
        result = { ...result, telemetryStale: prevStale };
      }
      if (prevOfflineNote !== undefined) {
        result = { ...result, offlineNote: prevOfflineNote };
      }
      return result;
    });
    setBeacons(withStatus);
    setBeaconsLoaded(true);
  } catch (error) {
    console.error('Error fetching beacons:', error);
  }
};
