import { useEffect, useRef } from 'react';

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

/**
 * Refreshes the map's data when the tab or device comes back after being hidden long enough to be stale.
 *
 * refresh re-fetches the data and rejects when the server cannot be reached. A failed refresh is only
 * logged: a device that has just woken often has no network yet, and reloading the page then would replace
 * a stale map with a browser error page. The SignalR connection refreshes the data when it reconnects.
 */
export function useStaleRefresh(
  refresh: () => Promise<void>,
  opts: StaleRefreshOptions = {}
) {
  const options = { ...defaultOpts, ...opts };
  const lastHiddenAt = useRef<number | null>(null);
  const refreshInProgress = useRef<boolean>(false);
  const refreshRef = useRef(refresh);
  refreshRef.current = refresh;

  useEffect(() => {
    async function softRefresh() {
      if (refreshInProgress.current) return; // Avoid overlapping
      refreshInProgress.current = true;
      try {
        await refreshRef.current();
      } catch (e) {
        console.warn('Refresh after returning to the tab failed; waiting for the live connection:', e);
      } finally {
        refreshInProgress.current = false;
      }
    }

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

      softRefresh();
    }

    document.addEventListener('visibilitychange', onVisibilityChange);
    return () => document.removeEventListener('visibilitychange', onVisibilityChange);
  }, [options.hiddenThresholdMsMobile, options.hiddenThresholdMsDesktop, options.hardReloadFallbackMs, options.enableHardReload]);
}
