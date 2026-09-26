import { describe, expect, it } from 'vitest';
import {
  validateTelemetryStaleHoursOverride,
  canEditOfflineNote,
  groupByBeacon,
  availableSubdivisions,
  validateMilepost,
  describeCoordinateRelation
} from './AdminBeaconRailroads';
import type { AdminBeaconRailroad } from '../types/AdminBeaconRailroad';
import type { Subdivision } from '../types/Subdivision';

/** Minimal beacon railroad row; only the fields the helpers read are meaningful. */
function row(
  beaconID: number,
  beaconName: string,
  subdivisionID: number,
  subdivisionName: string,
  milepost: number
): AdminBeaconRailroad {
  return {
    beaconID,
    beaconName,
    subdivisionID,
    subdivisionName,
    railroadID: 1,
    railroadName: 'CN',
    latitude: 44.589494,
    longitude: -89.761417,
    milepost,
    multipleTracks: false,
    online: true,
    direction: 'All'
  };
}

function subdivision(id: number, name: string): Subdivision {
  return {
    id,
    railroadID: 1,
    railroad: 'CN',
    dpuCapable: false,
    name,
    createdAt: '',
    lastUpdate: ''
  };
}

// Junction City: one railroad crossing its own tracks via two subdivisions (issue #80).
const junctionCitySuperior = row(10, 'Junction City', 4, 'Superior', 260.0);
const junctionCityValley = row(10, 'Junction City', 5, 'Valley', 90.4);
const mosinee = row(11, 'Mosinee', 5, 'Valley', 78.5);

describe('validateTelemetryStaleHoursOverride', () => {
  it('returns null for null (no override)', () => {
    expect(validateTelemetryStaleHoursOverride(null)).toBeNull();
  });

  it('returns null for undefined (no override)', () => {
    expect(validateTelemetryStaleHoursOverride(undefined)).toBeNull();
  });

  it('returns null for a valid positive integer', () => {
    expect(validateTelemetryStaleHoursOverride(6)).toBeNull();
    expect(validateTelemetryStaleHoursOverride(1)).toBeNull();
    expect(validateTelemetryStaleHoursOverride(24)).toBeNull();
  });

  it('returns an error for zero', () => {
    const error = validateTelemetryStaleHoursOverride(0);
    expect(error).toBe('Telemetry stale hours override must be a whole integer greater than zero');
  });

  it('returns an error for a negative value', () => {
    const error = validateTelemetryStaleHoursOverride(-1);
    expect(error).toBe('Telemetry stale hours override must be a whole integer greater than zero');
  });

  it('returns an error for a non-integer (float)', () => {
    const error = validateTelemetryStaleHoursOverride(1.5);
    expect(error).toBe('Telemetry stale hours override must be a whole integer greater than zero');
  });

  it('returns an error for a negative float', () => {
    const error = validateTelemetryStaleHoursOverride(-0.5);
    expect(error).toBe('Telemetry stale hours override must be a whole integer greater than zero');
  });
});

describe('canEditOfflineNote', () => {
  it('allows admins regardless of subdivision custodian', () => {
    expect(canEditOfflineNote(true, false, 999, 1)).toBe(true);
    expect(canEditOfflineNote(true, false, null, 1)).toBe(true);
  });

  it('allows a custodian assigned to the subdivision', () => {
    expect(canEditOfflineNote(false, true, 50, 50)).toBe(true);
  });

  it('denies a custodian not assigned to the subdivision', () => {
    expect(canEditOfflineNote(false, true, 999, 50)).toBe(false);
  });

  it('denies a custodian when the subdivision has no assigned custodian', () => {
    expect(canEditOfflineNote(false, true, null, 50)).toBe(false);
    expect(canEditOfflineNote(false, true, undefined, 50)).toBe(false);
  });

  it('denies a user who is neither admin nor custodian', () => {
    expect(canEditOfflineNote(false, false, 50, 50)).toBe(false);
  });
});


describe('groupByBeacon', () => {
  it('returns an empty array for no rows', () => {
    expect(groupByBeacon([])).toEqual([]);
  });

  it('groups a single-subdivision beacon into one group of one row', () => {
    const groups = groupByBeacon([mosinee]);

    expect(groups).toHaveLength(1);
    expect(groups[0].beaconID).toBe(11);
    expect(groups[0].beaconName).toBe('Mosinee');
    expect(groups[0].rows).toHaveLength(1);
  });

  it('groups a junction’s subdivisions under one beacon', () => {
    const groups = groupByBeacon([junctionCitySuperior, junctionCityValley]);

    expect(groups).toHaveLength(1);
    expect(groups[0].beaconID).toBe(10);
    expect(groups[0].rows.map(r => r.subdivisionName)).toEqual(['Superior', 'Valley']);
    expect(groups[0].rows.map(r => r.milepost)).toEqual([260.0, 90.4]);
  });

  it('keeps a beacon’s rows together even when they are not adjacent in the input', () => {
    const groups = groupByBeacon([junctionCitySuperior, mosinee, junctionCityValley]);

    expect(groups).toHaveLength(2);
    expect(groups[0].beaconID).toBe(10);
    expect(groups[0].rows).toHaveLength(2);
    expect(groups[1].beaconID).toBe(11);
    expect(groups[1].rows).toHaveLength(1);
  });

  it('preserves the incoming order of groups and of rows within a group', () => {
    const groups = groupByBeacon([mosinee, junctionCityValley, junctionCitySuperior]);

    expect(groups.map(g => g.beaconID)).toEqual([11, 10]);
    expect(groups[1].rows.map(r => r.subdivisionName)).toEqual(['Valley', 'Superior']);
  });
});

describe('availableSubdivisions', () => {
  const all = [subdivision(4, 'Superior'), subdivision(5, 'Valley'), subdivision(3, 'Neenah')];

  it('returns every subdivision when the beacon has no rows yet', () => {
    expect(availableSubdivisions(all, []).map(s => s.id)).toEqual([4, 5, 3]);
  });

  it('filters out subdivisions the beacon already has', () => {
    const available = availableSubdivisions(all, [junctionCitySuperior]);

    expect(available.map(s => s.id)).toEqual([5, 3]);
  });

  it('returns an empty list when every subdivision is assigned', () => {
    const rows = [junctionCitySuperior, junctionCityValley, row(10, 'Junction City', 3, 'Neenah', 12.0)];

    expect(availableSubdivisions(all, rows)).toEqual([]);
  });

  it('keeps the edited row’s own subdivision so the select can display it', () => {
    const available = availableSubdivisions(all, [junctionCitySuperior, junctionCityValley], 4);

    expect(available.map(s => s.id)).toEqual([4, 3]);
  });
});

describe('validateMilepost', () => {
  const expected = 'Milepost is required and must be a number greater than zero';

  it('returns null for a valid milepost', () => {
    expect(validateMilepost(78.5)).toBeNull();
    expect(validateMilepost(260)).toBeNull();
    expect(validateMilepost(0.1)).toBeNull();
  });

  it('returns an error for NaN, which is what a cleared number input produces', () => {
    expect(validateMilepost(NaN)).toBe(expected);
  });

  it('returns an error for null or undefined', () => {
    expect(validateMilepost(null)).toBe(expected);
    expect(validateMilepost(undefined)).toBe(expected);
  });

  it('returns an error for zero or a negative milepost', () => {
    expect(validateMilepost(0)).toBe(expected);
    expect(validateMilepost(-5)).toBe(expected);
  });

  it('returns an error for a non-finite value', () => {
    expect(validateMilepost(Infinity)).toBe(expected);
  });
});

describe('describeCoordinateRelation', () => {
  /** Row at explicit coordinates, for comparing one beacon's subdivisions against each other. */
  function rowAt(
    subdivisionID: number,
    subdivisionName: string,
    railroadName: string,
    latitude: number,
    longitude: number
  ): AdminBeaconRailroad {
    return {
      ...row(1, 'Beacon', subdivisionID, subdivisionName, 100),
      railroadName,
      latitude,
      longitude
    };
  }

  const superior = rowAt(4, 'Superior', 'CN', 44.589494, -89.761417);
  const waukesha = rowAt(1, 'Waukesha', 'CN', 43.280951, -88.214687);
  const milwaukee = rowAt(2, 'Milwaukee', 'WSOR', 43.277704, -88.211869);

  it('returns null when the beacon has no other rows', () => {
    expect(describeCoordinateRelation({ latitude: 44.589494, longitude: -89.761417 }, [])).toBeNull();
  });

  it('returns null for an unfilled coordinate pair', () => {
    expect(describeCoordinateRelation({ latitude: 0, longitude: 0 }, [superior])).toBeNull();
  });

  it('returns null for a cleared input, which parses to NaN', () => {
    expect(describeCoordinateRelation({ latitude: NaN, longitude: -89.761417 }, [superior])).toBeNull();
  });

  it('identifies a junction as the same point', () => {
    const notice = describeCoordinateRelation(
      { latitude: 44.589494, longitude: -89.761417 },
      [superior]
    );

    expect(notice?.severity).toBe('info');
    expect(notice?.message).toContain('Same location as CN Superior');
    expect(notice?.message).toContain('junction');
  });

  it('identifies parallel tracks as a separate point and names the distance', () => {
    const notice = describeCoordinateRelation(
      { latitude: milwaukee.latitude, longitude: milwaukee.longitude },
      [waukesha]
    );

    expect(notice?.severity).toBe('info');
    expect(notice?.message).toContain('1,401 ft from CN Waukesha');
    expect(notice?.message).toContain('Separate track');
  });

  it('warns on a separation too small to be a separate track', () => {
    // Junction City's Valley row, entered by hand with one wrong digit.
    const notice = describeCoordinateRelation(
      { latitude: 44.589594, longitude: -89.761417 },
      [superior]
    );

    expect(notice?.severity).toBe('warning');
    expect(notice?.message).toContain('CN Superior');
    expect(notice?.message).toContain('mistyped digit');
  });

  it('compares against the nearest row when the beacon has several', () => {
    const notice = describeCoordinateRelation(
      { latitude: milwaukee.latitude, longitude: milwaukee.longitude },
      [superior, waukesha]
    );

    // Superior is over a hundred miles away; Waukesha is the meaningful comparison.
    expect(notice?.message).toContain('CN Waukesha');
    expect(notice?.message).not.toContain('Superior');
  });

  it('names the railroad so two subdivisions of different railroads stay distinct', () => {
    const notice = describeCoordinateRelation(
      { latitude: waukesha.latitude, longitude: waukesha.longitude },
      [milwaukee]
    );

    expect(notice?.message).toContain('WSOR Milwaukee');
  });
});
