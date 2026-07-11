import { describe, expect, it } from 'vitest';
import type { MapPin } from '../types/MapPin';
import { removeMapPin, updateMapPins } from './updateHelpers';

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
