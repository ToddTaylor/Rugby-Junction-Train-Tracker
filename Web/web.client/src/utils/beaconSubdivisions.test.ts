import { describe, expect, it } from 'vitest';
import { latestBeaconUpdate, markerSubdivisionIDs, pinAtMarker } from './beaconSubdivisions';
import type { Beacon } from '../types/Beacon';

// Junction City collapsed: the Valley row is the representative, Superior carries the traffic.
const junctionCity: Beacon = {
  beaconID: '10',
  beaconName: 'Junction City',
  railroadID: '1',
  subdivisionID: '5',
  latitude: 44.589494,
  longitude: -89.761417,
  milepost: 63.2,
  online: true,
  subdivisions: [
    { subdivisionID: '4', subdivision: 'Superior', railroadID: '1', milepost: 260 },
    { subdivisionID: '5', subdivision: 'Valley', railroadID: '1', milepost: 63.2 }
  ]
};

const mosinee: Beacon = {
  beaconID: '11',
  beaconName: 'Mosinee',
  railroadID: '1',
  subdivisionID: '5',
  latitude: 44.804505,
  longitude: -89.680821,
  milepost: 78.5,
  online: true
};

describe('markerSubdivisionIDs', () => {
  it('lists every subdivision of a junction', () => {
    expect(markerSubdivisionIDs(junctionCity)).toEqual(['4', '5']);
  });

  it('falls back to the row’s own subdivision', () => {
    expect(markerSubdivisionIDs(mosinee)).toEqual(['5']);
  });
});

describe('latestBeaconUpdate', () => {
  it('reports a train on the subdivision the marker row does not name', () => {
    const entry = latestBeaconUpdate({ '10|4': { lastUpdate: '2026-10-06T18:56:00Z', direction: 'E' } }, junctionCity);

    expect(entry).toEqual({ lastUpdate: '2026-10-06T18:56:00Z', direction: 'E' });
  });

  it('picks the most recent train across subdivisions', () => {
    const entry = latestBeaconUpdate({
      '10|4': { lastUpdate: '2026-10-06T18:56:00Z', direction: 'E' },
      '10|5': { lastUpdate: '2026-10-06T12:00:00Z', direction: 'N' }
    }, junctionCity);

    expect(entry?.direction).toBe('E');
  });

  it('returns undefined when no subdivision has a train', () => {
    expect(latestBeaconUpdate({ '11|5': { lastUpdate: '2026-10-06T18:56:00Z' } }, junctionCity)).toBeUndefined();
    expect(latestBeaconUpdate(undefined, junctionCity)).toBeUndefined();
  });
});

describe('pinAtMarker', () => {
  it('matches a pin on any of a junction’s subdivisions', () => {
    expect(pinAtMarker({ beaconID: '10', subdivisionID: '4' }, junctionCity)).toBe(true);
    expect(pinAtMarker({ beaconID: '10', subdivisionID: '5' }, junctionCity)).toBe(true);
  });

  it('rejects a pin at another beacon or subdivision', () => {
    expect(pinAtMarker({ beaconID: '11', subdivisionID: '4' }, junctionCity)).toBe(false);
    expect(pinAtMarker({ beaconID: '11', subdivisionID: '4' }, mosinee)).toBe(false);
  });
});
