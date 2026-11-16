import { useCallback, useEffect, useState } from 'react';
import { Beacon } from '../types/Beacon';
import { applyStoredStatus, fetchBeaconsFromServer, loadBeacons } from '../api/beaconsApi';
import {
  BEACON_SERVER_STATUS_MAP_KEY,
  BEACON_STALE_MAP_KEY,
  BEACON_STATUS_MAP_KEY,
  beaconStatusKey,
  clearLegacyBeaconMaps,
  type StoredServerStatus
} from '../utils/beaconStatusKeys';

export function useBeacons() {
  const [beacons, setBeacons] = useState<Beacon[]>([]);
  const [beaconsLoaded, setBeaconsLoaded] = useState(false);

  // Cached list first, then the server's list. See loadBeacons.
  useEffect(() => {
    let cancelled = false;
    clearLegacyBeaconMaps();

    loadBeacons(loaded => {
      if (!cancelled) {
        setBeacons(loaded);
        setBeaconsLoaded(true);
      }
    }).catch(error => console.error('Error fetching beacons:', error));

    return () => { cancelled = true; };
  }, []);

  /**
   * Replaces the list with the server's. Rejects when the server cannot be reached, leaving the list as it was.
   */
  const refreshBeacons = useCallback(async () => {
    const fresh = await fetchBeaconsFromServer();
    setBeacons(applyStoredStatus(fresh));
    setBeaconsLoaded(true);
  }, []);

  // Persist status map whenever beacons array changes (includes SignalR updates)
  useEffect(() => {
    if (!beacons.length) return;
    const statusMap: Record<string, boolean> = {};
    const staleMap: Record<string, boolean> = {};
    const serverStatusMap: Record<string, StoredServerStatus> = {};
    beacons.forEach(b => {
      if (b && b.beaconID) {
        const key = beaconStatusKey(b.beaconID, b.subdivisionID);
        statusMap[key] = !!b.online;
        staleMap[key] = !!b.telemetryStale;
        if (b.serverStatus) {
          serverStatusMap[key] = { serverStatus: b.serverStatus, serverStatusReasons: b.serverStatusReasons ?? [] };
        }
      }
    });
    try {
      localStorage.setItem(BEACON_STATUS_MAP_KEY, JSON.stringify(statusMap));
      localStorage.setItem(BEACON_STALE_MAP_KEY, JSON.stringify(staleMap));
      localStorage.setItem(BEACON_SERVER_STATUS_MAP_KEY, JSON.stringify(serverStatusMap));
    } catch { /* ignore quota */ }
  }, [beacons]);

  return { beacons, beaconsLoaded, setBeacons, refreshBeacons };
}
