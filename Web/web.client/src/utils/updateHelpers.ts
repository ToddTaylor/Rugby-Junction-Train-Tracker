import { Beacon } from "../types/Beacon";
import { Address } from "../types/Address";
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

    const matchingPins = pins.filter(mapPin => {
        if (String(mapPin.id) === String(newPin.id)) {
            return true;
        }

        if (sharesIdentity(mapPin)) {
            return true;
        }

        return sharesAddress(mapPin);
    });

    const mergedPin = mergeMatchingPins(matchingPins, newPin);

    const remaining = pins.filter(mapPin => {
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

    return [...remaining, mergedPin];
}

export function updateBeacon(beacons: Beacon[], updatedBeacon: Beacon): Beacon[] {
    const remaining = beacons.filter(
        (beacon) => !(String(beacon.beaconID) === String(updatedBeacon.beaconID) && String(beacon.railroadID) === String(updatedBeacon.railroadID))
    );

    return [...remaining, updatedBeacon];
}

function mergeMatchingPins(matchingPins: MapPin[], newPin: MapPin): MapPin {
    if (matchingPins.length === 0) {
        return newPin;
    }

    const mergedAddresses = dedupeAddresses([
        ...matchingPins.flatMap(pin => pin.addresses ?? []),
        ...(newPin.addresses ?? []),
    ]);

    const mergedSourceTypes = dedupeSourceTypes([
        ...matchingPins.flatMap(pin => pin.addressSourceTypes ?? []),
        ...(newPin.addressSourceTypes ?? []),
        ...mergedAddresses.map(address => address.source),
    ]);

    return {
        ...newPin,
        shareCode: newPin.shareCode ?? matchingPins.find(pin => pin.shareCode)?.shareCode,
        addresses: mergedAddresses.length > 0 ? mergedAddresses : newPin.addresses,
        addressSourceTypes: mergedSourceTypes,
        hasDpu: mergedSourceTypes.includes("DPU") || newPin.hasDpu || matchingPins.some(pin => pin.hasDpu),
    };
}

function dedupeAddresses(addresses: Address[]): Address[] {
    const deduped = new Map<string, Address>();

    addresses.forEach(address => {
        deduped.set(`${address.addressID}|${address.source}`, address);
    });

    return [...deduped.values()];
}

function dedupeSourceTypes(sourceTypes: string[]): string[] {
    return [...new Set(
        sourceTypes
            .map(sourceType => sourceType?.trim().toUpperCase())
            .filter((sourceType): sourceType is string => !!sourceType)
    )].sort();
}
