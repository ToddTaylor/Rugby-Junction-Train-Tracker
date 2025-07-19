import { useState, useEffect } from 'react';
import { openDB } from 'idb';

export function useRailways() {
  const [trackData, setTracksData] = useState<GeoJSON.GeoJsonObject | null>(null);
  const [trackDataLoaded, setTrackDataLoaded] = useState(false);

  useEffect(() => {
    const fetchRailways = async () => {
      const STORE_NAME = 'geojson';
      const DB_VERSION = 2;
      const db = await openDB('railways-db', DB_VERSION, {
        upgrade(db) {
          if (!db.objectStoreNames.contains('geojson')) {
            db.createObjectStore('geojson');
          }
          if (!db.objectStoreNames.contains('beacons')) {
            db.createObjectStore('beacons');
          }
        }
      });
      const cached = await db.get(STORE_NAME, 'railways');
      if (cached) {
        setTracksData(cached);
        setTrackDataLoaded(true);
        return;
      }
      const response = await fetch('/data/overpass-wi-railways.geojson');
      if (!response.ok) throw new Error('Failed to fetch railways');
      const data = await response.json();
      await db.put(STORE_NAME, data, 'railways');
      setTracksData(data);
      setTrackDataLoaded(true);
    };
    fetchRailways();
  }, []);

  return { trackData, trackDataLoaded };
}
