import { describe, expect, it } from 'vitest';
import type { MapPin } from '../types/MapPin';
import type { Beacon } from '../types/Beacon';
import { removeMapPin, updateMapPins, updateBeacon } from './updateHelpers';

function createMapPin(overrides: Partial<MapPin> = {}): MapPin {
    return {
        id: '101',
        shareCode: 'ABC123',
        beaconID: '1',
        beaconName: 'Rugby Jct',
        latitude: 43,
        longitude: -88,
        milepost: 117.2,
        direction: 'Northbound',
        moving: true,
        isLocal: false,
        railroad: 'CN',
        subdivision: 'Waukesha Sub',
        subdivisionID: '10',
        lastUpdate: '2026-07-11T08:43:00Z',
        addresses: [
            { addressID: 12345, source: 'HOT', isActive: true },
        ],
        addressSourceTypes: ['HOT'],
        hasDpu: false,
        ...overrides,
    };
}

describe('updateMapPins', () => {
    it('replaces an existing pin only when IDs match', () => {
        const existingPin = createMapPin({ id: '101', addresses: [{ addressID: 11111, source: 'HOT', isActive: true }] });
        const unchangedPin = createMapPin({ id: '202', beaconID: '2', shareCode: 'ZZZ999' });
        const incomingPin = createMapPin({ id: '101', addresses: [{ addressID: 22222, source: 'EOT', isActive: true }] });

        const updated = updateMapPins([existingPin, unchangedPin], incomingPin);

        expect(updated).toHaveLength(2);
        expect(updated.find(p => p.id === '101')?.addresses).toEqual(incomingPin.addresses);
        expect(updated.find(p => p.id === '202')).toEqual(unchangedPin);
    });

    it('appends a new pin when ID does not exist', () => {
        const existingPin = createMapPin({ id: '101' });
        const incomingPin = createMapPin({ id: '303', shareCode: 'NEW303' });

        const updated = updateMapPins([existingPin], incomingPin);

        expect(updated).toHaveLength(2);
        expect(updated.find(p => p.id === '303')).toEqual(incomingPin);
    });
});

describe('removeMapPin', () => {
    it('removes only the targeted map pin ID', () => {
        const firstPin = createMapPin({ id: '101' });
        const secondPin = createMapPin({ id: '202' });

        const updated = removeMapPin([firstPin, secondPin], 101);

        expect(updated).toHaveLength(1);
        expect(updated[0].id).toBe('202');
    });
});


function createBeacon(overrides: Partial<Beacon> = {}): Beacon {
    return {
        beaconID: '10',
        beaconName: 'Junction City',
        railroadID: '1',
        railroad: 'CN',
        subdivisionID: '4',
        subdivision: 'Superior',
        latitude: 44.589494,
        longitude: -89.761417,
        milepost: 260.0,
        online: true,
        ...overrides,
    };
}

describe('updateBeacon', () => {
    // Junction City: one railroad (CN) crossing its own tracks via two subdivisions.
    const superior = createBeacon({ subdivisionID: '4', subdivision: 'Superior', milepost: 260.0 });
    const valley = createBeacon({ subdivisionID: '5', subdivision: 'Valley', milepost: 63.2 });

    it('replaces only the matching subdivision row', () => {
        const updated = updateBeacon([superior, valley], { ...superior, online: false });

        expect(updated).toHaveLength(2);
        expect(updated.find(b => b.subdivisionID === '4')?.online).toBe(false);
        expect(updated.find(b => b.subdivisionID === '5')).toEqual(valley);
    });

    it('keeps a junction’s other subdivision milepost intact', () => {
        const updated = updateBeacon([superior, valley], { ...valley, online: false });

        expect(updated.map(b => b.milepost).sort((a, b) => a - b)).toEqual([63.2, 260.0]);
    });

    it('appends a new subdivision row for a beacon already present', () => {
        const updated = updateBeacon([superior], valley);

        expect(updated).toHaveLength(2);
        expect(updated.find(b => b.subdivisionID === '5')).toEqual(valley);
    });

    it('does not disturb a different beacon', () => {
        const mosinee = createBeacon({ beaconID: '11', beaconName: 'Mosinee', subdivisionID: '5', subdivision: 'Valley', milepost: 78.5 });
        const updated = updateBeacon([superior, mosinee], { ...superior, online: false });

        expect(updated).toHaveLength(2);
        expect(updated.find(b => b.beaconID === '11')).toEqual(mosinee);
    });
});
