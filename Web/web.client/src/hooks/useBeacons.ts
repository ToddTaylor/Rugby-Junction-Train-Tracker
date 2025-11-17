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
      let cached;
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
        const { data: beacons } = await response.json();
        await db.put(STORE_NAME, beacons, 'beacons');
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
