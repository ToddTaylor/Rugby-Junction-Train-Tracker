import { Beacon } from "../types/Beacon";
import { MapPin } from "../types/MapPin";

export function updateMapPins(pins: MapPin[], newPin: MapPin): MapPin[] {
    const existingIndex = pins.findIndex(
        (mapPin) =>
            Array.isArray(mapPin.addresses) &&
            mapPin.addresses.some(addr1 =>
                Array.isArray(newPin.addresses) &&
                newPin.addresses.some(addr2 =>
                    addr1.addressID === addr2.addressID && addr1.source === addr2.source
                )
            )
    );

    if (existingIndex !== -1) {
        pins.splice(existingIndex, 1);
    }
    return [...pins, newPin];
}

export function updateBeacon(beacons: Beacon[], updatedBeacon: Beacon): Beacon[] {
    const existingIndex = beacons.findIndex(
        (beacon) => beacon.beaconID === updatedBeacon.beaconID && beacon.railroadID === updatedBeacon.railroadID
    );

    if (existingIndex !== -1) {
        beacons.splice(existingIndex, 1);
    }
    return [...beacons, updatedBeacon];
}
