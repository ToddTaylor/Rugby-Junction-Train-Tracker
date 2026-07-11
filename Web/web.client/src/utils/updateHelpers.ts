import { Beacon } from "../types/Beacon";
import { MapPin } from "../types/MapPin";

export function updateMapPins(pins: MapPin[], newPin: MapPin): MapPin[] {
    const remaining = pins.filter(mapPin => String(mapPin.id) !== String(newPin.id));
    return [...remaining, newPin];
}

export function removeMapPin(pins: MapPin[], mapPinId: string | number): MapPin[] {
    return pins.filter(mapPin => String(mapPin.id) !== String(mapPinId));
}

export function updateBeacon(beacons: Beacon[], updatedBeacon: Beacon): Beacon[] {
    const remaining = beacons.filter(
        (beacon) => !(String(beacon.beaconID) === String(updatedBeacon.beaconID) && String(beacon.railroadID) === String(updatedBeacon.railroadID))
    );

    return [...remaining, updatedBeacon];
}
