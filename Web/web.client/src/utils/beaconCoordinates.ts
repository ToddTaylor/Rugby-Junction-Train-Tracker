/**
 * Coordinate comparison for the beacon railroad rows of a single beacon.
 *
 * One beacon carries a row per subdivision, and the coordinates of those rows say which of two
 * physical shapes the location has:
 *
 * - A junction, where one railroad crosses its own tracks, is a single point. Every row sits at
 *   the same coordinates and only the milepost differs between them.
 * - Parallel tracks belonging to different railroads are separate points. Each row carries its
 *   own coordinates so a map pin lands on the track the train is actually on.
 *
 * Both shapes are valid, so coordinates stay per row. The distance between a row and its
 * siblings is what distinguishes them, and a small non-zero distance indicates neither shape:
 * it is a mistyped digit in a value meant to match.
 */

/** Separation below which two rows of one beacon are treated as a transcription error. */
export const COINCIDENT_THRESHOLD_FEET = 50;

const EARTH_RADIUS_MILES = 3958.8;
const FEET_PER_MILE = 5280;

export interface Coordinates {
    latitude: number;
    longitude: number;
}

export type CoordinateRelation = 'same' | 'nearby' | 'separate';

export interface CoordinateComparison {
    relation: CoordinateRelation;
    feet: number;
}

/**
 * Great-circle distance in feet. Used over short spans where the curvature term is negligible
 * but the cosine scaling of longitude is not.
 */
export function distanceInFeet(from: Coordinates, to: Coordinates): number {
    const fromLatRad = toRadians(from.latitude);
    const toLatRad = toRadians(to.latitude);
    const deltaLat = toLatRad - fromLatRad;
    const deltaLon = toRadians(to.longitude - from.longitude);

    const h =
        Math.sin(deltaLat / 2) ** 2 +
        Math.cos(fromLatRad) * Math.cos(toLatRad) * Math.sin(deltaLon / 2) ** 2;

    return 2 * EARTH_RADIUS_MILES * Math.asin(Math.min(1, Math.sqrt(h))) * FEET_PER_MILE;
}

/**
 * Classifies how one row's coordinates relate to a sibling row's.
 *
 * Exact equality reads as 'same' rather than testing against a tolerance, because a junction's
 * rows are filled from one another and so hold identical values. 'nearby' covers the gap between
 * that and a real second track.
 */
export function compareCoordinates(a: Coordinates, b: Coordinates): CoordinateComparison {
    if (a.latitude === b.latitude && a.longitude === b.longitude) {
        return { relation: 'same', feet: 0 };
    }

    const feet = distanceInFeet(a, b);

    return {
        relation: feet < COINCIDENT_THRESHOLD_FEET ? 'nearby' : 'separate',
        feet
    };
}

/** Formats a separation for display, in feet up to a mile and in miles beyond it. */
export function formatSeparation(feet: number): string {
    if (feet < FEET_PER_MILE) {
        return `${Math.round(feet).toLocaleString()} ft`;
    }

    return `${(feet / FEET_PER_MILE).toFixed(1)} mi`;
}

function toRadians(degrees: number): number {
    return (degrees * Math.PI) / 180;
}
