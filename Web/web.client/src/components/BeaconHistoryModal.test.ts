import { describe, expect, it } from 'vitest';
import { formatDirectionAbbreviation, getPrimaryLocalToggleAddress, resolveHistorySubdivisionID } from './BeaconHistoryModal';
import type { AddressSnapshot } from '../types/MapPinHistory';
import type { BeaconSubdivision } from '../types/Beacon';

describe('formatDirectionAbbreviation', () => {
    it.each([
        ['N', 'N'],
        ['S', 'S'],
        ['E', 'E'],
        ['W', 'W'],
        ['NE', 'NE'],
        ['NW', 'NW'],
        ['SE', 'SE'],
        ['SW', 'SW'],
    ])('returns the compass abbreviation for %s', (input, expected) => {
        expect(formatDirectionAbbreviation(input)).toBe(expected);
    });

    it('is case-insensitive', () => {
        expect(formatDirectionAbbreviation('nw')).toBe('NW');
    });

    it('returns "?" when direction is missing', () => {
        expect(formatDirectionAbbreviation(undefined)).toBe('?');
        expect(formatDirectionAbbreviation(null)).toBe('?');
        expect(formatDirectionAbbreviation('')).toBe('?');
    });

    it('passes through unrecognized values as-is', () => {
        expect(formatDirectionAbbreviation('XYZ')).toBe('XYZ');
    });
});

function makeAddress(overrides: Partial<AddressSnapshot> = {}): AddressSnapshot {
    return {
        addressID: 1,
        source: 'HOT',
        createdAt: '2026-01-01T00:00:00Z',
        lastUpdate: '2026-01-01T00:00:00Z',
        ...overrides,
    };
}

describe('getPrimaryLocalToggleAddress', () => {
    it('returns the first address when multiple are present', () => {
        const addresses = [makeAddress({ addressID: 5 }), makeAddress({ addressID: 9 })];
        expect(getPrimaryLocalToggleAddress(addresses)?.addressID).toBe(5);
    });

    it('returns the single address when only one is present', () => {
        const addresses = [makeAddress({ addressID: 42 })];
        expect(getPrimaryLocalToggleAddress(addresses)?.addressID).toBe(42);
    });

    it('returns undefined when addresses is empty', () => {
        expect(getPrimaryLocalToggleAddress([])).toBeUndefined();
    });

    it('returns undefined when addresses is not an array', () => {
        expect(getPrimaryLocalToggleAddress(undefined)).toBeUndefined();
    });
});

describe('resolveHistorySubdivisionID', () => {
    function sub(subdivisionID: string, subdivision: string, railroad: string): BeaconSubdivision {
        return { subdivisionID, subdivision, railroadID: '1', railroad, milepost: 100 };
    }

    // Junction City: CN crossing its own tracks. Both rows collapse to one marker, which
    // reports whichever subdivision was processed last.
    const junctionCity = [sub('4', 'Superior', 'CN'), sub('10', 'Valley', 'CN')];

    it('requests the whole beacon at a junction so no subdivision is hidden', () => {
        expect(resolveHistorySubdivisionID('10', junctionCity)).toBeUndefined();
    });

    it('requests the whole beacon whichever subdivision the marker happens to report', () => {
        expect(resolveHistorySubdivisionID('4', junctionCity)).toBeUndefined();
    });

    it('keeps the subdivision filter for a beacon with one subdivision', () => {
        expect(resolveHistorySubdivisionID('10', [sub('10', 'Valley', 'CN')])).toBe('10');
    });

    it('keeps the subdivision filter when no subdivisions were gathered', () => {
        expect(resolveHistorySubdivisionID('10', undefined)).toBe('10');
        expect(resolveHistorySubdivisionID('10', [])).toBe('10');
    });

    it('keeps parallel tracks separate, since each railroad has its own marker', () => {
        // Rugby Junction: one location, two railroads, two markers, one subdivision each.
        expect(resolveHistorySubdivisionID('1', [sub('1', 'Waukesha', 'CN')])).toBe('1');
        expect(resolveHistorySubdivisionID('2', [sub('2', 'Milwaukee', 'WSOR')])).toBe('2');
    });

    it('passes through an undefined subdivision unchanged', () => {
        expect(resolveHistorySubdivisionID(undefined, undefined)).toBeUndefined();
    });
});
