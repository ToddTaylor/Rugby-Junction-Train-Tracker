import { describe, expect, it } from 'vitest';
import { MAX_WEBCAMS, normalizeEntries, padEntries, parseChannelRef, parseVideoId } from './webcamConfig';

describe('parseVideoId', () => {
    it('accepts a bare video ID', () => {
        expect(parseVideoId(' NDeeYpmuGmI ')).toBe('NDeeYpmuGmI');
    });

    it('extracts the ID from common video URLs', () => {
        expect(parseVideoId('https://www.youtube.com/watch?v=NDeeYpmuGmI&t=10')).toBe('NDeeYpmuGmI');
        expect(parseVideoId('https://youtu.be/NDeeYpmuGmI?si=abc')).toBe('NDeeYpmuGmI');
        expect(parseVideoId('youtube.com/live/NDeeYpmuGmI')).toBe('NDeeYpmuGmI');
        expect(parseVideoId('https://www.youtube.com/embed/NDeeYpmuGmI')).toBe('NDeeYpmuGmI');
        expect(parseVideoId('https://m.youtube.com/watch?v=NDeeYpmuGmI')).toBe('NDeeYpmuGmI');
    });

    it('returns null for search text and channel URLs', () => {
        expect(parseVideoId('Slinger Depot Museum Live Railcam')).toBeNull();
        expect(parseVideoId('https://www.youtube.com/@SteelHighway/live')).toBeNull();
        expect(parseVideoId('https://example.com/watch?v=NDeeYpmuGmI')).toBeNull();
        expect(parseVideoId('')).toBeNull();
    });
});

describe('parseChannelRef', () => {
    it('recognizes handle and channel ID URLs', () => {
        expect(parseChannelRef('https://www.youtube.com/@SteelHighway/live')).toEqual({ handle: '@SteelHighway' });
        expect(parseChannelRef('youtube.com/channel/UCJId-kbfsO5K4kU9cKE_TqA')).toEqual({ channelId: 'UCJId-kbfsO5K4kU9cKE_TqA' });
    });

    it('returns null for anything else', () => {
        expect(parseChannelRef('https://www.youtube.com/watch?v=NDeeYpmuGmI')).toBeNull();
        expect(parseChannelRef('Slinger Depot Museum Live Railcam')).toBeNull();
    });
});

describe('normalizeEntries', () => {
    it('trims values and drops empty rows', () => {
        expect(normalizeEntries([
            { source: '  Slinger ', videoId: ' NDeeYpmuGmI ' },
            { source: '', videoId: '' },
            { source: 'Waukesha', videoId: '' }
        ])).toEqual([
            { source: 'Slinger', videoId: 'NDeeYpmuGmI' },
            { source: 'Waukesha', videoId: '' }
        ]);
    });

    it('caps the list at the maximum', () => {
        const many = Array.from({ length: 20 }, (_, i) => ({ source: `Cam ${i}`, videoId: '' }));
        expect(normalizeEntries(many)).toHaveLength(MAX_WEBCAMS);
    });

    it('rejects malformed input', () => {
        expect(normalizeEntries('nope')).toBeNull();
        expect(normalizeEntries([null])).toBeNull();
    });
});

describe('padEntries', () => {
    it('fills the list to the maximum with empty rows', () => {
        const rows = padEntries([{ source: 'Slinger', videoId: '' }]);
        expect(rows).toHaveLength(MAX_WEBCAMS);
        expect(rows[1]).toEqual({ source: '', videoId: '' });
    });
});
