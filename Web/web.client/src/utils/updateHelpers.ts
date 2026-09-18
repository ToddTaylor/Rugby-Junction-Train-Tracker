import { Beacon } from "../types/Beacon";
import { MapPin } from "../types/MapPin";

export function updateMapPins(pins: MapPin[], newPin: MapPin): MapPin[] {
    const remaining = pins.filter(mapPin => String(mapPin.id) !== String(newPin.id));
    return [...remaining, newPin];
}

export function removeMapPin(pins: MapPin[], mapPinId: string | number): MapPin[] {
    return pins.filter(mapPin => String(mapPin.id) !== String(mapPinId));
}

/**
 * Replaces a single beacon railroad row, keyed by beacon AND subdivision.
 *
 * A beacon railroad is uniquely identified by (beaconID, subdivisionID) - its composite
 * primary key - not by railroad. Matching on railroad alone would drop every subdivision a
 * railroad runs through the location: at a junction such as Junction City, where the CN
 * Superior and CN Valley subdivisions cross, both rows share a railroad and one update would
 * collapse them into a single row, losing a milepost (see issue #80).
 */
export function updateBeacon(beacons: Beacon[], updatedBeacon: Beacon): Beacon[] {
    const remaining = beacons.filter(
        (beacon) => !(String(beacon.beaconID) === String(updatedBeacon.beaconID) && String(beacon.subdivisionID) === String(updatedBeacon.subdivisionID))
    );

    return [...remaining, updatedBeacon];
}
