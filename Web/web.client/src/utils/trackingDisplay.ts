import type { MapPin } from '../types/MapPin';
import type { TrackedPin } from '../services/trackedPins';

/** What a train marker shows about the user's tracked pin for it. */
export interface TrackingDisplay {
    isTracked: boolean;
    trackColor?: string;
    trackedSymbol?: string;
}

/**
 * Looks up how each train should show the user's tracked pins. A train counts as tracked, and takes its color,
 * from a tracked pin with the same ID; its symbol comes from that pin or, failing that, one with the same share code.
 * Each marker then gets plain values, so a change to one tracked pin re-renders only the train it belongs to.
 */
export function buildTrackingLookup(trackedPins: TrackedPin[]): (pin: MapPin) => TrackingDisplay {
    const byId = new Map<string, TrackedPin>();
    const byShareCode = new Map<string, TrackedPin>();
    for (const trackedPin of trackedPins) {
        const id = String(trackedPin.id);
        if (!byId.has(id)) byId.set(id, trackedPin);
        if (trackedPin.shareCode && !byShareCode.has(trackedPin.shareCode)) byShareCode.set(trackedPin.shareCode, trackedPin);
    }

    return pin => {
        const tracked = byId.get(String(pin.id));
        const forSymbol = tracked ?? (pin.shareCode ? byShareCode.get(pin.shareCode) : undefined);
        return {
            isTracked: !!tracked,
            trackColor: tracked?.color,
            trackedSymbol: forSymbol?.symbol,
        };
    };
}

/**
 * True when two tracked pin lists hold the same data. The map re-reads tracked pins from storage every few
 * seconds; keeping the old array when nothing changed stops each read from re-rendering the whole map.
 */
export function sameTrackedPins(a: TrackedPin[], b: TrackedPin[]): boolean {
    return a === b || JSON.stringify(a) === JSON.stringify(b);
}
