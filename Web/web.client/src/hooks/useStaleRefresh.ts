import { useEffect, useRef } from 'react';
import { fetchInitialTelemetryPins } from '../api/mapPinsApi';
import { fetchBeacons } from '../api/beaconsApi';
import { Beacon } from '../types/Beacon';
import { MapPin } from '../types/MapPin';

export interface StaleRefreshOptions {
  hiddenThresholdMsMobile?: number; // time hidden before refresh attempt on mobile
  hiddenThresholdMsDesktop?: number; // time hidden before refresh attempt on desktop
  hardReloadFallbackMs?: number; // if hidden longer than this always hard reload
  enableHardReload?: boolean; // allow hard reload fallback
}

const defaultOpts: StaleRefreshOptions = {
  hiddenThresholdMsMobile: 60_000,
  hiddenThresholdMsDesktop: 180_000,
  hardReloadFallbackMs: 15 * 60_000, // 15 minutes
  enableHardReload: true,
};

function isMobileUA(): boolean {
  if (typeof navigator === 'undefined') return false;
  return /android|iphone|ipad|ipod|mobile/i.test(navigator.userAgent);
}

// Hook to perform stale-aware refresh when returning from hidden state.
// Expects caller to provide setters for pins and beacons.
export function useStaleRefresh(
  setMapPins: (pins: MapPin[]) => void,
  setBeacons: (beacons: Beacon[]) => void,
  setBeaconsLoaded: (loaded: boolean) => void,
  opts: StaleRefreshOptions = {}
) {
  const options = { ...defaultOpts, ...opts };
  const lastHiddenAt = useRef<number | null>(null);
  const refreshInProgress = useRef<boolean>(false);

  async function softRefresh(): Promise<boolean> {
    if (refreshInProgress.current) return true; // Avoid overlapping
    refreshInProgress.current = true;
    try {
      // Re-fetch telemetry pins
      await fetchInitialTelemetryPins(setMapPins);
      // Re-fetch beacons (beacons seldom change but safe)
      await fetchBeacons(setBeacons, setBeaconsLoaded);
      return true;
    } catch (e) {
      console.error('Soft refresh failed:', e);
      return false;
    } finally {
      refreshInProgress.current = false;
    }
  }

  useEffect(() => {
    function onVisibilityChange() {
      if (document.hidden) {
        lastHiddenAt.current = Date.now();
        return;
      }
      // Became visible
      if (!lastHiddenAt.current) return;
      const hiddenDuration = Date.now() - lastHiddenAt.current;
      lastHiddenAt.current = null;

      // Set grace period (retain prior online statuses briefly to avoid flicker)
      try { localStorage.setItem('focusGraceUntil', String(Date.now() + 5000)); } catch { /* ignore */ }

      const threshold = isMobileUA() ? options.hiddenThresholdMsMobile! : options.hiddenThresholdMsDesktop!;

      if (hiddenDuration < threshold) {
        return; // Not stale
      }

      // If hidden for extremely long, optionally hard reload immediately
      if (options.enableHardReload && hiddenDuration >= options.hardReloadFallbackMs!) {
        window.location.reload();
        return;
      }

      // Attempt soft refresh first
      softRefresh().then(success => {
        if (!success && options.enableHardReload) {
          window.location.reload();
        }
      });
    }

    document.addEventListener('visibilitychange', onVisibilityChange);
    return () => document.removeEventListener('visibilitychange', onVisibilityChange);
  }, [options.hiddenThresholdMsMobile, options.hiddenThresholdMsDesktop, options.hardReloadFallbackMs, options.enableHardReload]);
}
