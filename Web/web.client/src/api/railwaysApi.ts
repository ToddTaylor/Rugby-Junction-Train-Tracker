import { openRailwaysDB } from './db';

// Fetch and cache railways GeoJSON (legacy path; worker-based hook preferred for main usage)
export const fetchRailways = async (setTracksData: any, setTrackDataLoaded: any) => {
  const STORE_NAME = 'geojson';
  const db = await openRailwaysDB();
  const cached = await db.get(STORE_NAME, 'railways');
  if (cached) {
    setTracksData(cached);
    setTrackDataLoaded(true);
    return;
  }
  const response = await fetch('/data/usdot-wisconsin-no-fields.geojson');
  if (!response.ok) throw new Error('Failed to fetch railways');
  const data = await response.json();
  await db.put(STORE_NAME, data, 'railways');
  setTracksData(data);
  setTrackDataLoaded(true);
};
