import { MapPinHistory } from '../types/MapPinHistory';

type CacheEntry<T> = {
    at: number;
    data: T;
};

const CACHE_TTL_MS = 15_000; // 15 seconds to smooth quick re-opens / re-renders

// Cache by key: beaconID|subdivisionID|limit
const historyCache = new Map<string, CacheEntry<MapPinHistory[]>>();

function makeKey(beaconID: string | number, subdivisionID?: string | number, limit?: number) {
    return `${beaconID}${subdivisionID ? `|${subdivisionID}` : ''}${limit ? `|${limit}` : ''}`;
}

function isFresh<T>(entry?: CacheEntry<T>, ttlMs = CACHE_TTL_MS) {
    if (!entry) return false;
    return Date.now() - entry.at < ttlMs;
}

function buildHistoryUrl(beaconID: string | number, subdivisionID?: string | number, limit = 10) {
    let apiUrl = `${import.meta.env.VITE_API_URL}/api/v1/MapPins/History/${beaconID}?limit=${limit}&t=${Date.now()}`;
    if (subdivisionID) apiUrl += `&subdivisionId=${subdivisionID}`;
    return apiUrl;
}

export async function fetchBeaconHistory(
    beaconID: string | number,
    subdivisionID?: string | number,
    limit = 10,
    options?: { ttlMs?: number; bypassCache?: boolean }
): Promise<MapPinHistory[]> {
    const ttl = options?.ttlMs ?? CACHE_TTL_MS;
    const bypass = options?.bypassCache ?? false;

    const key = makeKey(beaconID, subdivisionID, limit);

    if (!bypass && isFresh(historyCache.get(key), ttl)) {
        return historyCache.get(key)!.data;
    }

    // If asking for limit=1, try to satisfy from any larger cached list first
    if (limit === 1 && !bypass) {
        // Check for a cached entry with limit>1 for same beacon/subdivision
        for (const [k, entry] of historyCache.entries()) {
            if (!isFresh(entry, ttl)) continue;
            const [b, s, l] = k.split('|');
            if (String(b) === String(beaconID) && String(s || '') === String(subdivisionID || '') && Number(l) > 1) {
                const list = entry.data || [];
                const first = list[0] ? [list[0]] : [];
                if (first.length) {
                    // Also cache under the limit=1 key
                    historyCache.set(key, { at: Date.now(), data: first });
                    return first;
                }
            }
        }
    }

    const url = buildHistoryUrl(beaconID, subdivisionID, limit);
    const response = await fetch(url, {
        headers: {
            'X-Api-Key': import.meta.env.VITE_API_KEY,
            'Content-Type': 'application/json'
        }
    });
    if (!response.ok) throw new Error(`Failed to fetch beacon history (${response.status})`);

    const { data } = await response.json();
    const list: MapPinHistory[] = Array.isArray(data) ? data : [];
    historyCache.set(key, { at: Date.now(), data: list });
    return list;
}

export function invalidateBeaconHistoryCache(beaconID: string | number, subdivisionID?: string | number) {
    // Remove all cache entries for this beacon/subdivision combination
    const keysToDelete: string[] = [];
    for (const key of historyCache.keys()) {
        const [b, s] = key.split('|');
        if (String(b) === String(beaconID) && String(s || '') === String(subdivisionID || '')) {
            keysToDelete.push(key);
        }
    }
    keysToDelete.forEach(key => historyCache.delete(key));
}

export async function fetchBeaconLatestFromHistory(
    beaconID: string | number,
    subdivisionID?: string | number,
    options?: { ttlMs?: number; bypassCache?: boolean }
): Promise<{ lastUpdate: string | null; direction: string | null } | null> {
    // Prefer fulfilling from any cached list with limit>1 if available
    const ttl = options?.ttlMs ?? CACHE_TTL_MS;
    for (const [k, entry] of historyCache.entries()) {
        if (!isFresh(entry, ttl)) continue;
        const [b, s, l] = k.split('|');
        if (String(b) === String(beaconID) && String(s || '') === String(subdivisionID || '') && Number(l) > 1) {
            const first = entry.data?.[0];
            if (first) return { lastUpdate: first.lastUpdate ?? null, direction: (first as any).direction ?? null };
        }
    }

    const list = await fetchBeaconHistory(beaconID, subdivisionID, 1, options);
    const latest = list[0];
    if (!latest) return null;
    return { lastUpdate: latest.lastUpdate ?? null, direction: (latest as any).direction ?? null };
}
