import { describe, expect, it } from 'vitest';
import {
  COINCIDENT_THRESHOLD_FEET,
  compareCoordinates,
  distanceInFeet,
  formatSeparation
} from './beaconCoordinates';

// Coordinates of the beacons that carry more than one subdivision row.
const junctionCitySuperior = { latitude: 44.589494, longitude: -89.761417 };
const junctionCityValley = { latitude: 44.589494, longitude: -89.761417 };
const rugbyWaukesha = { latitude: 43.280951, longitude: -88.214687 };
const rugbyMilwaukee = { latitude: 43.277704, longitude: -88.211869 };
const sussexWaukesha = { latitude: 43.159517, longitude: -88.200492 };
const sussexAdams = { latitude: 43.137439, longitude: -88.209657 };

describe('distanceInFeet', () => {
  it('returns zero for one point', () => {
    expect(distanceInFeet(junctionCitySuperior, junctionCitySuperior)).toBe(0);
  });

  it('measures a known separation between two parallel tracks', () => {
    // Rugby Junction's two railroads sit about a quarter mile apart.
    expect(distanceInFeet(rugbyWaukesha, rugbyMilwaukee)).toBeCloseTo(1401, -2);
  });

  it('measures a separation over a mile', () => {
    expect(distanceInFeet(sussexWaukesha, sussexAdams)).toBeCloseTo(8416, -2);
  });

  it('is symmetric', () => {
    const forward = distanceInFeet(rugbyWaukesha, rugbyMilwaukee);
    const backward = distanceInFeet(rugbyMilwaukee, rugbyWaukesha);

    expect(forward).toBeCloseTo(backward, 6);
  });

  it('scales longitude by latitude', () => {
    // A degree of longitude is shorter near the pole than at the equator.
    const atEquator = distanceInFeet({ latitude: 0, longitude: 0 }, { latitude: 0, longitude: 1 });
    const atSixty = distanceInFeet({ latitude: 60, longitude: 0 }, { latitude: 60, longitude: 1 });

    expect(atSixty).toBeLessThan(atEquator * 0.55);
  });
});

describe('compareCoordinates', () => {
  it('reports a junction as the same point', () => {
    const result = compareCoordinates(junctionCitySuperior, junctionCityValley);

    expect(result.relation).toBe('same');
    expect(result.feet).toBe(0);
  });

  it('reports parallel tracks as separate', () => {
    expect(compareCoordinates(rugbyWaukesha, rugbyMilwaukee).relation).toBe('separate');
    expect(compareCoordinates(sussexWaukesha, sussexAdams).relation).toBe('separate');
  });

  it('reports a mistyped digit as nearby', () => {
    // A junction row filled from its sibling, then given a wrong digit in the sixth decimal
    // place, lands a few feet away rather than on the same point.
    const mistyped = { latitude: 44.589594, longitude: -89.761417 };
    const result = compareCoordinates(junctionCitySuperior, mistyped);

    expect(result.relation).toBe('nearby');
    expect(result.feet).toBeGreaterThan(0);
    expect(result.feet).toBeLessThan(COINCIDENT_THRESHOLD_FEET);
  });

  it('treats the threshold as the boundary between nearby and separate', () => {
    // Roughly 60 ft north, just past the threshold.
    const justPast = { latitude: 44.589494 + 0.00017, longitude: -89.761417 };
    const result = compareCoordinates(junctionCitySuperior, justPast);

    expect(result.feet).toBeGreaterThan(COINCIDENT_THRESHOLD_FEET);
    expect(result.relation).toBe('separate');
  });

  it('reports a transposed sign as separate rather than nearby', () => {
    const wrongHemisphere = { latitude: 44.589494, longitude: 89.761417 };

    expect(compareCoordinates(junctionCitySuperior, wrongHemisphere).relation).toBe('separate');
  });
});

describe('formatSeparation', () => {
  it('reports feet below a mile', () => {
    expect(formatSeparation(1401)).toBe('1,401 ft');
    expect(formatSeparation(12.4)).toBe('12 ft');
  });

  it('reports miles at a mile and beyond', () => {
    expect(formatSeparation(5280)).toBe('1.0 mi');
    expect(formatSeparation(8416)).toBe('1.6 mi');
  });
});
