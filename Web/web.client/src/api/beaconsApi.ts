import { openRailwaysDB } from './db';
import { fetchWithAuth } from '../utils/fetchWithAuth';

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

  let statusMap: Record<string, boolean> = {};
  try {
    const raw = localStorage.getItem('beaconStatusMap');
    if (raw) statusMap = JSON.parse(raw);
  } catch { /* ignore */ }
  const graceUntil = Number(localStorage.getItem('focusGraceUntil') || '0');
  const now = Date.now();

  if (cached) {
    const withStatus = (cached as any[]).map(b => {
      const prevOnline = statusMap[b.beaconID];
      if (prevOnline === true && b.online === false && now < graceUntil) {
        return { ...b, online: true };
      }
      return prevOnline === undefined ? b : { ...b, online: prevOnline };
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
      if (prevOnline === true && b.online === false && now < graceUntil) {
        return { ...b, online: true };
      }
      return prevOnline === undefined ? b : { ...b, online: prevOnline };
    });
    setBeacons(withStatus);
    setBeaconsLoaded(true);
  } catch (error) {
    console.error('Error fetching beacons:', error);
  }
};
