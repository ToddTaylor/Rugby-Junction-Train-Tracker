import { useState, useEffect } from 'react';
import { openDB } from 'idb';
import { Beacon } from '../types/Beacon';

export function useBeacons() {
  const [beacons, setBeacons] = useState<Beacon[]>([]);
  const [beaconsLoaded, setBeaconsLoaded] = useState(false);
  // Load persisted beacon statuses once at module init
  let initialStatusMap: Record<string, boolean> = {};
  try {
    const raw = localStorage.getItem('beaconStatusMap');
    if (raw) initialStatusMap = JSON.parse(raw);
  } catch { /* ignore malformed storage */ }

  useEffect(() => {
    const fetchBeacons = async () => {
      const STORE_NAME = 'beacons';
      const DB_VERSION = 2;
      // Cache schema version - increment when beacon data structure changes
      // This ensures stale cached data without new fields (like railroad/subdivision names) is refreshed
      const BEACON_CACHE_VERSION = 2; // v2: added railroad/subdivision name fields
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
          const stored = initialStatusMap[b.beaconID];
          if (stored === true && b.online === false && now < graceUntil) {
            return { ...b, online: true };
          }
          return stored === undefined ? b : { ...b, online: stored };
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
          online: b.online
        }));
        
        // Store beacons and cache version together
        await db.put(STORE_NAME, beacons, 'beacons');
        await db.put(STORE_NAME, BEACON_CACHE_VERSION, CACHE_VERSION_KEY);
        
        const withStatus = (beacons as Beacon[]).map(b => {
          const stored = initialStatusMap[b.beaconID];
          if (stored === true && b.online === false && now < graceUntil) {
            return { ...b, online: true };
          }
          return stored === undefined ? b : { ...b, online: stored };
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
    beacons.forEach(b => { if (b && b.beaconID) statusMap[b.beaconID] = !!b.online; });
    try { localStorage.setItem('beaconStatusMap', JSON.stringify(statusMap)); } catch { /* ignore quota */ }
  }, [beacons]);

  return { beacons, beaconsLoaded, setBeacons };
}
