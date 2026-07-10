import { describe, expect, it } from 'vitest';
import type { MapPin } from '../types/MapPin';
import { updateMapPins } from './updateHelpers';

function createMapPin(overrides: Partial<MapPin> = {}): MapPin {
  return {
    id: '101',
    shareCode: 'UUEF6D',
    beaconID: '1',
    beaconName: 'Rugby Jct',
    latitude: 43.0,
    longitude: -88.0,
    milepost: 117.2,
    direction: 'Northbound',
    moving: true,
    isLocal: false,
    railroad: 'CN',
    subdivision: 'Waukesha Sub',
    subdivisionID: '10',
    lastUpdate: '2026-07-10T08:43:00Z',
    addresses: [
      { addressID: 12345, source: 'HOT', isActive: false },
      { addressID: 67890, source: 'EOT', isActive: true },
    ],
    addressSourceTypes: ['HOT', 'EOT'],
    hasDpu: false,
    ...overrides,
  };
}

describe('updateMapPins', () => {
  it('preserves richer support addresses when a live update arrives without them', () => {
    const existingPin = createMapPin();
    const incomingPin = createMapPin({
      addresses: [],
      addressSourceTypes: [],
    });

    const updatedPins = updateMapPins([existingPin], incomingPin);

    expect(updatedPins).toHaveLength(1);
    expect(updatedPins[0].addresses).toEqual(existingPin.addresses);
    expect(updatedPins[0].addressSourceTypes).toEqual(['EOT', 'HOT']);
  });

  it('merges duplicate-beacon address lists for the same live pin identity', () => {
    const existingPin = createMapPin({
      id: '101',
      shareCode: 'UUEF6D',
      addresses: [{ addressID: 12345, source: 'HOT', isActive: true }],
      addressSourceTypes: ['HOT'],
    });
    const incomingPin = createMapPin({
      id: '202',
      shareCode: 'UUEF6D',
      addresses: [
        { addressID: 67890, source: 'EOT', isActive: true },
        { addressID: 24680, source: 'DPU', isActive: false },
      ],
      addressSourceTypes: [],
      hasDpu: false,
    });

    const updatedPins = updateMapPins([existingPin], incomingPin);

    expect(updatedPins).toHaveLength(1);
    expect(updatedPins[0].addresses).toEqual([
      { addressID: 12345, source: 'HOT', isActive: true },
      { addressID: 67890, source: 'EOT', isActive: true },
      { addressID: 24680, source: 'DPU', isActive: false },
    ]);
    expect(updatedPins[0].addressSourceTypes).toEqual(['DPU', 'EOT', 'HOT']);
    expect(updatedPins[0].hasDpu).toBe(true);
  });
});
