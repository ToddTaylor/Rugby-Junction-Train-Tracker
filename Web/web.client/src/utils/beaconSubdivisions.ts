import { Beacon } from '../types/Beacon';

/** The fields that identify which beacon and subdivisions a marker stands for. */
export type MarkerLocation = Pick<Beacon, 'beaconID' | 'subdivisions'> & { subdivisionID?: string };

/**
 * Every subdivision ID a marker stands for: all of a collapsed junction's, or the row's own.
 */
export function markerSubdivisionIDs(pin: MarkerLocation): string[] {
    const ids = pin.subdivisions?.length
        ? pin.subdivisions.map(s => String(s.subdivisionID))
        : [String(pin.subdivisionID ?? '')];
    return Array.from(new Set(ids));
}

/**
 * The most recent last-train entry across every subdivision a marker stands for, so a junction
 * reports the latest train whichever subdivision it ran on.
 */
export function latestBeaconUpdate<T extends { lastUpdate: string }>(
    lastUpdateMap: { [key: string]: T } | undefined,
    pin: MarkerLocation
): T | undefined {
    if (!lastUpdateMap || !pin.beaconID) return undefined;
    return markerSubdivisionIDs(pin)
        .map(subdivisionID => lastUpdateMap[`${pin.beaconID}${subdivisionID ? `|${subdivisionID}` : ''}`])
        .filter((entry): entry is T => !!entry?.lastUpdate)
        .reduce<T | undefined>(
            (latest, entry) => !latest || new Date(entry.lastUpdate) > new Date(latest.lastUpdate) ? entry : latest,
            undefined
        );
}

/**
 * Whether a map pin was recorded at the location a marker stands for, on any of its subdivisions.
 */
export function pinAtMarker(pin: { beaconID?: string | number; subdivisionID?: string | number }, marker: MarkerLocation): boolean {
    return String(pin.beaconID || '') === String(marker.beaconID || '')
        && markerSubdivisionIDs(marker).includes(String(pin.subdivisionID || ''));
}
