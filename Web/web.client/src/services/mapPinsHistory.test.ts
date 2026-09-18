import { describe, expect, it, beforeEach, vi, afterEach } from 'vitest';
import { fetchBeaconHistory, invalidateBeaconHistoryCache } from './mapPinsHistory';

vi.mock('./auth', () => ({
    getAuthToken: () => Promise.resolve('test-token')
}));

/** Responds to every history request with one row, counting the calls that reach the network. */
function mockFetch() {
    const calls: string[] = [];

    vi.stubGlobal('fetch', vi.fn((url: string) => {
        calls.push(url);
        return Promise.resolve({
            ok: true,
            json: () => Promise.resolve({ data: [{ id: calls.length, beaconID: 17 }] })
        });
    }));

    return calls;
}

describe('fetchBeaconHistory', () => {
    beforeEach(() => {
        // Each test starts with an empty cache for the beacons it uses.
        invalidateBeaconHistoryCache(17);
        invalidateBeaconHistoryCache(18);
    });

    afterEach(() => {
        vi.unstubAllGlobals();
    });

    it('omits the subdivision from the query when none is given', async () => {
        const calls = mockFetch();

        await fetchBeaconHistory(17, undefined, 10);

        expect(calls[0]).toContain('/History/17');
        expect(calls[0]).not.toContain('subdivisionId');
    });

    it('includes the subdivision when one is given', async () => {
        const calls = mockFetch();

        await fetchBeaconHistory(17, 4, 10);

        expect(calls[0]).toContain('subdivisionId=4');
    });

    it('caches a beacon-wide request separately from a per-subdivision one', async () => {
        const calls = mockFetch();

        await fetchBeaconHistory(17, undefined, 10);
        await fetchBeaconHistory(17, 4, 10);

        // Different keys, so the second request is not served from the first's entry.
        expect(calls).toHaveLength(2);
    });

    it('serves a repeated request from the cache', async () => {
        const calls = mockFetch();

        await fetchBeaconHistory(17, undefined, 10);
        await fetchBeaconHistory(17, undefined, 10);

        expect(calls).toHaveLength(1);
    });
});

describe('invalidateBeaconHistoryCache', () => {
    beforeEach(() => {
        invalidateBeaconHistoryCache(17);
        invalidateBeaconHistoryCache(18);
    });

    afterEach(() => {
        vi.unstubAllGlobals();
    });

    it('clears a beacon-wide entry when telemetry arrives', async () => {
        const calls = mockFetch();

        // A junction's modal caches under the beacon alone.
        await fetchBeaconHistory(17, undefined, 10);
        expect(calls).toHaveLength(1);

        // Telemetry arrives for this beacon.
        invalidateBeaconHistoryCache(17);

        // The beacon-wide entry must be gone, or the junction serves a stale list.
        await fetchBeaconHistory(17, undefined, 10);
        expect(calls).toHaveLength(2);
    });

    it('clears every subdivision entry for the beacon', async () => {
        const calls = mockFetch();

        await fetchBeaconHistory(17, 4, 10);
        await fetchBeaconHistory(17, 10, 10);
        expect(calls).toHaveLength(2);

        invalidateBeaconHistoryCache(17);

        await fetchBeaconHistory(17, 4, 10);
        await fetchBeaconHistory(17, 10, 10);
        expect(calls).toHaveLength(4);
    });

    it('leaves other beacons cached', async () => {
        const calls = mockFetch();

        await fetchBeaconHistory(17, undefined, 10);
        await fetchBeaconHistory(18, undefined, 10);
        expect(calls).toHaveLength(2);

        invalidateBeaconHistoryCache(17);

        await fetchBeaconHistory(18, undefined, 10);
        expect(calls).toHaveLength(2);
    });
});
