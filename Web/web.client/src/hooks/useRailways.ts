import { useState, useEffect } from 'react';
import { openDB } from 'idb';

interface CachedRailways {
  version: string;
  data: GeoJSON.GeoJsonObject;
  timestamp: number;
}

export function useRailways() {
  const [trackData, setTracksData] = useState<GeoJSON.GeoJsonObject | null>(null);
  const [trackDataLoaded, setTrackDataLoaded] = useState(false);
  const [trackDataLoading, setTrackDataLoading] = useState(true);

  useEffect(() => {
    let aborted = false;
    let worker: Worker | null = null;

    const fetchRailways = async () => {
      setTrackDataLoading(true);
      const STORE_NAME = 'geojson';
      const DB_VERSION = 2;
      const RAILWAYS_VERSION = '2025-11-15-1';
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
      // idb's get<T> generic parameter represents the key type constraint, not the value type.
      // Retrieve then assert the value shape.
      const cached = (await db.get(STORE_NAME, 'railways')) as CachedRailways | undefined;
      if (cached && cached.version === RAILWAYS_VERSION && cached.data) {
        setTracksData(cached.data);
        setTrackDataLoaded(true);
        setTrackDataLoading(false);
        return;
      }

      worker = new Worker(new URL('../workers/railwaysWorker.js', import.meta.url), { type: 'module' });
      worker.onmessage = async (e: MessageEvent<any>) => {
        if (aborted) return;
        const { success, data, error } = e.data || {};
        if (!success || !data) {
          console.error('Railways worker failed:', error);
          setTrackDataLoaded(false);
          setTrackDataLoading(false);
          worker?.terminate();
          return;
        }
        try {
          const payload: CachedRailways = {
            version: RAILWAYS_VERSION,
            data,
            timestamp: Date.now()
          };
          await db.put(STORE_NAME, payload, 'railways');
        } catch (e) {
          console.warn('Failed to cache railways data in IndexedDB:', e);
        }
        setTracksData(data as GeoJSON.GeoJsonObject);
        setTrackDataLoaded(true);
        setTrackDataLoading(false);
        worker?.terminate();
      };
      worker.postMessage({});
    };

    fetchRailways();

    return () => {
      aborted = true;
      worker?.terminate();
    };
  }, []);

  return { trackData, trackDataLoaded, trackDataLoading };
}
