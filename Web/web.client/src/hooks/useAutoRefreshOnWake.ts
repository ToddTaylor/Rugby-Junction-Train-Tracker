import { useEffect, useRef } from 'react';

// Hook: Reload the page when it becomes visible again after being hidden (sleep/background)
// Useful on mobile browsers (Android/iOS) where timers may be throttled and data can go stale.
// thresholdMs: minimum time hidden before forcing a reload.
export function useAutoRefreshOnWake(thresholdMs: number = 60_000) {
  const lastHiddenAt = useRef<number | null>(null);

  useEffect(() => {
    function onVisibilityChange() {
      if (document.hidden) {
        lastHiddenAt.current = Date.now();
      } else {
        // Page became visible
        if (lastHiddenAt.current) {
          const hiddenDuration = Date.now() - lastHiddenAt.current;
          if (hiddenDuration >= thresholdMs) {
            // Force a hard reload to pull fresh assets & data
            window.location.reload();
          }
        }
        lastHiddenAt.current = null;
      }
    }

    document.addEventListener('visibilitychange', onVisibilityChange);
    return () => document.removeEventListener('visibilitychange', onVisibilityChange);
  }, [thresholdMs]);
}

// Optional helper: detect mobile (simple heuristic)
export function isLikelyMobile(): boolean {
  if (typeof navigator === 'undefined') return false;
  const ua = navigator.userAgent.toLowerCase();
  return /android|iphone|ipad|ipod|mobile/.test(ua);
}