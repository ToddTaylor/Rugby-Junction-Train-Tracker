import { useState, useEffect } from 'react';
import { openDB } from 'idb';
import { Beacon } from '../types/Beacon';
import {
  BEACON_OFFLINE_NOTE_MAP_KEY,
  BEACON_STALE_MAP_KEY,
  BEACON_STATUS_MAP_KEY,
  beaconStatusKey,
  clearLegacyBeaconMaps,
  readBeaconMap
} from '../utils/beaconStatusKeys';

export function useBeacons() {
  const [beacons, setBeacons] = useState<Beacon[]>([]);
  const [beaconsLoaded, setBeaconsLoaded] = useState(false);
  // Load persisted beacon statuses once at module init
  clearLegacyBeaconMaps();
  const initialStatusMap = readBeaconMap<boolean>(BEACON_STATUS_MAP_KEY);
  const initialStaleMap = readBeaconMap<boolean>(BEACON_STALE_MAP_KEY);
  const initialOfflineNoteMap = readBeaconMap<string | null>(BEACON_OFFLINE_NOTE_MAP_KEY);

  useEffect(() => {
    const fetchBeacons = async () => {
      const STORE_NAME = 'beacons';
      const DB_VERSION = 2;
      // Cache schema version - increment when beacon data structure changes
      // This ensures stale cached data without new fields (like railroad/subdivision names) is refreshed
      const BEACON_CACHE_VERSION = 5; // v5: per-subdivision mileposts at junctions — caches written before a beacon's second subdivision row existed would hide it (issue #80)
      const CACHE_VERSION_KEY = 'beacons_version';
      
      const db = await openDB('railways-db', DB_VERSION, {
        upgrade(db) {
          if (!db.objectStoreNames.contains('geojson')) {
            db.createObjectStore('geojson');
          }
          if (!db.objectStoreNames.contains(STORE_NAME)) {
            db.createObjectStore(STORE_NAME);
          }
        }
      });
      
      // Check if cached data is from an older schema version
      let cachedVersion: number | undefined;
      try {
        cachedVersion = await db.get(STORE_NAME, CACHE_VERSION_KEY);
      } catch { /* ignore */ }
      
      let cached;
      // Only use cache if version matches current schema version
      if (cachedVersion === BEACON_CACHE_VERSION) {
        try {
          cached = await db.get(STORE_NAME, 'beacons');
        } catch (e) {
          await db.close();
          await indexedDB.deleteDatabase('railways-db');
          const db2 = await openDB('railways-db', DB_VERSION, {
            upgrade(db) {
              if (!db.objectStoreNames.contains('geojson')) {
                db.createObjectStore('geojson');
              }
              if (!db.objectStoreNames.contains(STORE_NAME)) {
                db.createObjectStore(STORE_NAME);
              }
            }
          });
          cached = await db2.get(STORE_NAME, 'beacons');
        }
      }
      const graceUntil = Number(localStorage.getItem('focusGraceUntil') || '0');
      const now = Date.now();
      if (cached) {
        const withStatus = (cached as Beacon[]).map(b => {
          const key = beaconStatusKey(b.beaconID, b.subdivisionID);
          const stored = initialStatusMap[key];
          const storedStale = initialStaleMap[key];
          const storedOfflineNote = initialOfflineNoteMap[key];
          let result = b;
          if (stored === true && b.online === false && now < graceUntil) {
            result = { ...result, online: true };
          } else if (stored !== undefined) {
            result = { ...result, online: stored };
          }
          if (storedStale !== undefined) {
            result = { ...result, telemetryStale: storedStale };
          }
          if (storedOfflineNote !== undefined) {
            result = { ...result, offlineNote: storedOfflineNote };
          }
          return result;
        });
        setBeacons(withStatus);
        setBeaconsLoaded(true);
        return;
      }
      try {
        const apiUrl = import.meta.env.VITE_API_URL + "/api/v1/BeaconRailroads";
        const response = await fetch(apiUrl, {
          headers: {
            'X-Api-Key': import.meta.env.VITE_API_KEY,
            'Content-Type': 'application/json'
          }
        });
        if (!response.ok) throw new Error('Failed to fetch map pins');
        const { data: beaconsData } = await response.json();
        
        // Map API field names to Beacon type field names
        const beacons = (beaconsData as any[]).map((b: any) => ({
          beaconID: b.beaconID,
          beaconName: b.beaconName,
          railroadID: b.railroadID,
          subdivisionID: b.subdivisionID,
          railroad: b.railroadName, // Map railroadName -> railroad
          subdivision: b.subdivisionName, // Map subdivisionName -> subdivision
          latitude: b.latitude,
          longitude: b.longitude,
          milepost: b.milepost,
          online: b.online,
          telemetryStale: b.telemetryStale,
          offlineNote: b.offlineNote
        }));
        
        // Store beacons and cache version together
        await db.put(STORE_NAME, beacons, 'beacons');
        await db.put(STORE_NAME, BEACON_CACHE_VERSION, CACHE_VERSION_KEY);
        
        const withStatus = (beacons as Beacon[]).map(b => {
          const key = beaconStatusKey(b.beaconID, b.subdivisionID);
          const stored = initialStatusMap[key];
          const storedStale = initialStaleMap[key];
          const storedOfflineNote = initialOfflineNoteMap[key];
          let result = b;
          if (stored === true && b.online === false && now < graceUntil) {
            result = { ...result, online: true };
          } else if (stored !== undefined) {
            result = { ...result, online: stored };
          }
          if (storedStale !== undefined) {
            result = { ...result, telemetryStale: storedStale };
          }
          if (storedOfflineNote !== undefined) {
            result = { ...result, offlineNote: storedOfflineNote };
          }
          return result;
        });
        setBeacons(withStatus);
        setBeaconsLoaded(true);
      } catch (error) {
        console.error('Error fetching map pins:', error);
      }
    };
    fetchBeacons();
  }, []);

  // Persist status map whenever beacons array changes (includes SignalR updates)
  useEffect(() => {
    if (!beacons.length) return;
    const statusMap: Record<string, boolean> = {};
    const staleMap: Record<string, boolean> = {};
    const offlineNoteMap: Record<string, string | null> = {};
    beacons.forEach(b => {
      if (b && b.beaconID) {
        const key = beaconStatusKey(b.beaconID, b.subdivisionID);
        statusMap[key] = !!b.online;
        staleMap[key] = !!b.telemetryStale;
        offlineNoteMap[key] = b.offlineNote ?? null;
      }
    });
    try {
      localStorage.setItem(BEACON_STATUS_MAP_KEY, JSON.stringify(statusMap));
      localStorage.setItem(BEACON_STALE_MAP_KEY, JSON.stringify(staleMap));
      localStorage.setItem(BEACON_OFFLINE_NOTE_MAP_KEY, JSON.stringify(offlineNoteMap));
    } catch { /* ignore quota */ }
  }, [beacons]);

  return { beacons, beaconsLoaded, setBeacons };
}
