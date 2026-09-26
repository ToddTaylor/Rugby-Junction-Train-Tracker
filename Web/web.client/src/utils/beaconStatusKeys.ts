/**
 * Keys for the beacon state persisted in localStorage.
 *
 * A beacon railroad is identified by beacon and subdivision together, not by beacon alone.
 * A junction beacon, where one railroad crosses its own tracks, has a row per subdivision, and
 * telemetryStale differs between them: it measures train traffic on a given subdivision rather
 * than radio health. Keying on beacon alone gives those rows one shared slot, so the last row
 * written wins and its value is applied to every subdivision on the next load.
 */

/** Storage key for the per beacon railroad online map. */
export const BEACON_STATUS_MAP_KEY = 'beaconStatusMapV2';

/** Storage key for the per beacon railroad telemetry stale map. */
export const BEACON_STALE_MAP_KEY = 'beaconTelemetryStaleMapV2';

/** Storage key for the per beacon railroad offline note map. */
export const BEACON_OFFLINE_NOTE_MAP_KEY = 'beaconOfflineNoteMapV2';

/** Storage keys holding the beacon-only shape, removed on first read of the current shape. */
const LEGACY_KEYS = ['beaconStatusMap', 'beaconTelemetryStaleMap', 'beaconOfflineNoteMap'];

/**
 * Identifies one beacon railroad row. Falls back to the beacon alone when no subdivision is
 * supplied, which keeps rows distinct from the entries of a beacon that has several.
 */
export function beaconStatusKey(
    beaconID: string | number,
    subdivisionID: string | number | undefined | null
): string {
    return subdivisionID === undefined || subdivisionID === null || subdivisionID === ''
        ? String(beaconID)
        : `${beaconID}|${subdivisionID}`;
}

/**
 * Reads and parses a stored map, returning an empty object when absent or malformed.
 */
export function readBeaconMap<T>(storageKey: string): Record<string, T> {
    try {
        const raw = localStorage.getItem(storageKey);
        return raw ? (JSON.parse(raw) as Record<string, T>) : {};
    } catch {
        return {};
    }
}

/**
 * Discards maps written under the beacon-only key shape. Their values cannot be assigned to a
 * subdivision, so they are dropped rather than migrated; the next server response repopulates
 * the current maps.
 */
export function clearLegacyBeaconMaps(): void {
    try {
        LEGACY_KEYS.forEach(key => localStorage.removeItem(key));
    } catch {
        /* ignore storage errors */
    }
}
