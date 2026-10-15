import { describe, expect, it } from 'vitest';
import { getTrackOwners, isTrackSelected, summarizeOwners, UNKNOWN_OWNER } from './railroadOwners';

const line = (properties: Record<string, unknown>): GeoJSON.Feature => ({
    type: 'Feature',
    properties,
    geometry: { type: 'LineString', coordinates: [[0, 0], [1, 1]] },
});

describe('getTrackOwners', () => {
    it('returns every owner that has a value', () => {
        expect(getTrackOwners({ RROWNER1: 'CN', RROWNER2: 'UP', RROWNER3: null })).toEqual(['CN', 'UP']);
    });

    it('falls back to the unknown owner when none are recorded', () => {
        expect(getTrackOwners({})).toEqual([UNKNOWN_OWNER]);
        expect(getTrackOwners(null)).toEqual([UNKNOWN_OWNER]);
    });
});

describe('isTrackSelected', () => {
    it('selects nothing when no owners are selected', () => {
        expect(isTrackSelected({ RROWNER1: 'CN' }, new Set())).toBe(false);
    });

    it('selects a segment whose owner is selected', () => {
        expect(isTrackSelected({ RROWNER1: 'CN' }, new Set(['CN']))).toBe(true);
        expect(isTrackSelected({ RROWNER1: 'UP' }, new Set(['CN']))).toBe(false);
    });

    it('selects a jointly owned segment when either owner is selected', () => {
        expect(isTrackSelected({ RROWNER1: 'UP', RROWNER2: 'CN' }, new Set(['CN']))).toBe(true);
    });

    it('selects segments without an owner when the unknown owner is selected', () => {
        expect(isTrackSelected({}, new Set([UNKNOWN_OWNER]))).toBe(true);
    });
});

describe('summarizeOwners', () => {
    it('counts segments per owner and sorts by name', () => {
        const data: GeoJSON.FeatureCollection = {
            type: 'FeatureCollection',
            features: [
                line({ RROWNER1: 'UP' }),
                line({ RROWNER1: 'CN', RROWNER2: 'UP' }),
                line({}),
            ],
        };

        expect(summarizeOwners(data)).toEqual([
            { code: 'CN', name: 'Canadian National', segmentCount: 1 },
            { code: 'UP', name: 'Union Pacific', segmentCount: 2 },
            { code: UNKNOWN_OWNER, name: 'Unknown', segmentCount: 1 },
        ]);
    });

    it('returns nothing when there is no data', () => {
        expect(summarizeOwners(null)).toEqual([]);
    });
});
