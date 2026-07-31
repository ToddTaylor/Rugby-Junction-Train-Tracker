import { describe, expect, it } from 'vitest';
import { formatDirectionAbbreviation, getPrimaryLocalToggleAddress } from './BeaconHistoryModal';
import type { AddressSnapshot } from '../types/MapPinHistory';

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
