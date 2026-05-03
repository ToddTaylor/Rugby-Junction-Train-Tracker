import { Beacon } from "../types/Beacon";
import { MapPin } from "../types/MapPin";

export function updateMapPins(pins: MapPin[], newPin: MapPin): MapPin[] {
    const newAddresses = Array.isArray(newPin.addresses) ? newPin.addresses : [];

    const sharesIdentity = (mapPin: MapPin): boolean =>
        !!mapPin.shareCode && !!newPin.shareCode && mapPin.shareCode === newPin.shareCode;

    const sharesAddress = (mapPin: MapPin): boolean =>
        Array.isArray(mapPin.addresses) &&
        newAddresses.length > 0 &&
        mapPin.addresses.some(addr1 =>
            newAddresses.some(addr2 =>
                addr1.addressID === addr2.addressID && addr1.source === addr2.source
            )
        );

    const merged = pins.filter(mapPin => {
        // Primary identity: map pin ID from server.
        if (String(mapPin.id) === String(newPin.id)) {
            return false;
        }

        if (sharesIdentity(mapPin)) {
            return false;
        }

        // Secondary identity: overlapping address/source tuple.
        if (sharesAddress(mapPin)) {
            return false;
        }

        return true;
    });

    return [...merged, newPin];
}

export function updateBeacon(beacons: Beacon[], updatedBeacon: Beacon): Beacon[] {
    const remaining = beacons.filter(
        (beacon) => !(String(beacon.beaconID) === String(updatedBeacon.beaconID) && String(beacon.railroadID) === String(updatedBeacon.railroadID))
    );

    return [...remaining, updatedBeacon];
}
