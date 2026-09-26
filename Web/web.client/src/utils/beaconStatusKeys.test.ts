import { describe, expect, it, beforeEach } from 'vitest';
import {
  BEACON_STALE_MAP_KEY,
  beaconStatusKey,
  clearLegacyBeaconMaps,
  readBeaconMap
} from './beaconStatusKeys';

describe('beaconStatusKey', () => {
  it('distinguishes the subdivisions of one junction beacon', () => {
    const superior = beaconStatusKey('17', '4');
    const valley = beaconStatusKey('17', '10');

    expect(superior).not.toBe(valley);
  });

  it('accepts numbers and strings interchangeably', () => {
    expect(beaconStatusKey(17, 10)).toBe(beaconStatusKey('17', '10'));
  });

  it('falls back to the beacon alone when no subdivision is supplied', () => {
    expect(beaconStatusKey('17', undefined)).toBe('17');
    expect(beaconStatusKey('17', null)).toBe('17');
    expect(beaconStatusKey('17', '')).toBe('17');
  });

  it('keeps a junction beacon’s rows from sharing one slot', () => {
    // Writing both rows under beacon-only keys loses the first value.
    const collapsed: Record<string, boolean> = {};
    collapsed['17'] = false; // Valley: fresh
    collapsed['17'] = true;  // Superior: stale, overwrites
    expect(Object.keys(collapsed)).toHaveLength(1);

    const keyed: Record<string, boolean> = {};
    keyed[beaconStatusKey('17', '10')] = false;
    keyed[beaconStatusKey('17', '4')] = true;

    expect(Object.keys(keyed)).toHaveLength(2);
    expect(keyed[beaconStatusKey('17', '10')]).toBe(false);
    expect(keyed[beaconStatusKey('17', '4')]).toBe(true);
  });
});

describe('readBeaconMap', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('returns an empty object when the key is absent', () => {
    expect(readBeaconMap<boolean>(BEACON_STALE_MAP_KEY)).toEqual({});
  });

  it('returns an empty object when the stored value is malformed', () => {
    localStorage.setItem(BEACON_STALE_MAP_KEY, 'not json');

    expect(readBeaconMap<boolean>(BEACON_STALE_MAP_KEY)).toEqual({});
  });

  it('parses a stored map', () => {
    localStorage.setItem(BEACON_STALE_MAP_KEY, JSON.stringify({ '17|4': true }));

    expect(readBeaconMap<boolean>(BEACON_STALE_MAP_KEY)).toEqual({ '17|4': true });
  });
});

describe('clearLegacyBeaconMaps', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('removes maps stored under the beacon-only key shape', () => {
    localStorage.setItem('beaconStatusMap', JSON.stringify({ '17': true }));
    localStorage.setItem('beaconTelemetryStaleMap', JSON.stringify({ '17': true }));
    localStorage.setItem('beaconOfflineNoteMap', JSON.stringify({ '17': 'note' }));

    clearLegacyBeaconMaps();

    expect(localStorage.getItem('beaconStatusMap')).toBeNull();
    expect(localStorage.getItem('beaconTelemetryStaleMap')).toBeNull();
    expect(localStorage.getItem('beaconOfflineNoteMap')).toBeNull();
  });

  it('leaves the current maps in place', () => {
    localStorage.setItem(BEACON_STALE_MAP_KEY, JSON.stringify({ '17|4': true }));

    clearLegacyBeaconMaps();

    expect(readBeaconMap<boolean>(BEACON_STALE_MAP_KEY)).toEqual({ '17|4': true });
  });
});
