import { useState, useEffect } from 'react';
import { openDB } from 'idb';
import { Beacon } from '../types/types';

export function useBeacons() {
  const [beacons, setBeacons] = useState<Beacon[]>([]);
  const [beaconsLoaded, setBeaconsLoaded] = useState(false);

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
      if (cached) {
        setBeacons(cached);
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
        setBeacons(beacons);
        setBeaconsLoaded(true);
      } catch (error) {
        console.error('Error fetching map pins:', error);
      }
    };
    fetchBeacons();
  }, []);

  return { beacons, beaconsLoaded, setBeacons };
}
