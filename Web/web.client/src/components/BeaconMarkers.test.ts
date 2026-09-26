import { describe, expect, it } from 'vitest';
import { collapseBeaconPins } from './BeaconMarkers';
import type { Beacon } from '../types/Beacon';

function beacon(
  beaconID: string,
  beaconName: string,
  railroadID: string,
  railroad: string,
  subdivisionID: string,
  subdivision: string,
  milepost: number,
  latitude = 44.589494,
  longitude = -89.761417
): Beacon {
  return {
    beaconID,
    beaconName,
    railroadID,
    railroad,
    subdivisionID,
    subdivision,
    milepost,
    latitude,
    longitude,
    online: true
  };
}

// Junction City: one railroad (CN) crossing its own tracks via two subdivisions.
// Both rows share the same physical location, so they must collapse to one marker.
const junctionCityValley = beacon('10', 'Junction City', '1', 'CN', '5', 'Valley', 63.2);
const junctionCitySuperior = beacon('10', 'Junction City', '1', 'CN', '4', 'Superior', 260.0);

// Rugby Junction: one location, two DIFFERENT railroads running parallel.
const rugbyCn = beacon('1', 'Rugby Junction', '1', 'CN', '1', 'Waukesha', 117.2, 43.280958, -88.214682);
const rugbyWsor = beacon('1', 'Rugby Junction', '2', 'WSOR', '2', 'Milwaukee', 112.16, 43.280958, -88.213966);

const mosinee = beacon('11', 'Mosinee', '1', 'CN', '5', 'Valley', 78.5, 44.804505, -89.680821);

describe('collapseBeaconPins', () => {
  it('returns nothing for no rows', () => {
    expect(collapseBeaconPins([])).toEqual([]);
  });

  it('collapses a junction’s same-railroad subdivisions into one marker', () => {
    const markers = collapseBeaconPins([junctionCitySuperior, junctionCityValley]);

    expect(markers).toHaveLength(1);
    expect(markers[0].beaconName).toBe('Junction City');
  });

  it('keeps both of a junction’s mileposts rather than discarding one', () => {
    const markers = collapseBeaconPins([junctionCitySuperior, junctionCityValley]);

    expect(markers[0].subdivisions).toHaveLength(2);
    expect(markers[0].subdivisions?.map(s => s.subdivision)).toEqual(['Superior', 'Valley']);
    expect(markers[0].subdivisions?.map(s => s.milepost)).toEqual([260.0, 63.2]);
  });

  it('keeps two markers when one location serves two different railroads', () => {
    const markers = collapseBeaconPins([rugbyCn, rugbyWsor]);

    expect(markers).toHaveLength(2);
    expect(markers.map(m => m.railroad)).toEqual(['CN', 'WSOR']);
    expect(markers.every(m => m.subdivisions?.length === 1)).toBe(true);
  });

  it('attaches the subdivision to an ordinary single-subdivision beacon', () => {
    const markers = collapseBeaconPins([mosinee]);

    expect(markers).toHaveLength(1);
    expect(markers[0].subdivisions).toEqual([
      { subdivisionID: '5', subdivision: 'Valley', railroadID: '1', railroad: 'CN', milepost: 78.5 }
    ]);
  });

  it('does not double-count a subdivision that appears twice', () => {
    const markers = collapseBeaconPins([junctionCityValley, junctionCityValley]);

    expect(markers).toHaveLength(1);
    expect(markers[0].subdivisions).toHaveLength(1);
  });

  it('drops rows with a missing or zero beacon ID', () => {
    const invalid = { ...mosinee, beaconID: '0' };

    expect(collapseBeaconPins([invalid])).toEqual([]);
    expect(collapseBeaconPins([invalid, mosinee])).toHaveLength(1);
  });

  it('groups by beacon ID when the railroad ID is missing', () => {
    const noRailroad = { ...mosinee, railroadID: '0' };
    const markers = collapseBeaconPins([noRailroad]);

    expect(markers).toHaveLength(1);
    expect(markers[0].subdivisions).toHaveLength(1);
  });

  it('handles several beacons together without cross-contamination', () => {
    const markers = collapseBeaconPins([junctionCitySuperior, rugbyCn, junctionCityValley, mosinee, rugbyWsor]);

    // Junction City collapses to one; Rugby Junction stays two; Mosinee one.
    expect(markers).toHaveLength(4);

    const jc = markers.find(m => m.beaconName === 'Junction City');
    expect(jc?.subdivisions).toHaveLength(2);

    const mos = markers.find(m => m.beaconName === 'Mosinee');
    expect(mos?.subdivisions).toHaveLength(1);
  });
});
