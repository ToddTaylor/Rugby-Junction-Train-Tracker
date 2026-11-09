import { describe, expect, it } from 'vitest';
import { buildServerStatusPopupHtml, getBeaconVisualState } from './BeaconMarker';
import { BEACON_OFFLINE_COLOR } from '../constants/beaconColors';
import type { Beacon } from '../types/Beacon';

function makeBeacon(overrides: Partial<Beacon> = {}): Beacon {
    return {
        beaconID: '1',
        beaconName: 'Test Beacon',
        latitude: 43.0,
        longitude: -88.0,
        milepost: 100,
        online: true,
        railroadID: '1',
        subdivisionID: '1',
        telemetryStale: false,
        ...overrides,
    };
}

describe('getBeaconVisualState', () => {
    describe('healthy beacon (online, telemetry fresh)', () => {
        it('is not offline and not telemetry stale', () => {
            const state = getBeaconVisualState(makeBeacon({ online: true, telemetryStale: false }));
            expect(state.isOffline).toBe(false);
            expect(state.isTelemetryStale).toBe(false);
        });

        it('uses blue dot color', () => {
            const state = getBeaconVisualState(makeBeacon({ online: true, telemetryStale: false }));
            expect(state.color).toBe('#005aa9');
            expect(state.dotCenterColor).toBe('#005aa9');
        });

        it('reports title as online', () => {
            const state = getBeaconVisualState(makeBeacon({ online: true, telemetryStale: false }));
            expect(state.title).toBe('online');
        });
    });

    describe('health-offline beacon (gray dot)', () => {
        it('is offline regardless of telemetryStale flag', () => {
            const state = getBeaconVisualState(makeBeacon({ online: false, telemetryStale: false }));
            expect(state.isOffline).toBe(true);
            expect(state.isTelemetryStale).toBe(false);
        });

        it('uses gray color', () => {
            const state = getBeaconVisualState(makeBeacon({ online: false }));
            expect(state.color).toBe('#888888');
            expect(state.dotCenterColor).toBe('#888888');
        });

        it('reports title as offline with details available', () => {
            const state = getBeaconVisualState(makeBeacon({ online: false }));
            expect(state.title).toBe('offline - click for details');
        });

        it('is never telemetry-stale when offline, even if telemetryStale is true', () => {
            const state = getBeaconVisualState(makeBeacon({ online: false, telemetryStale: true }));
            expect(state.isOffline).toBe(true);
            expect(state.isTelemetryStale).toBe(false);
        });
    });

    describe('Server Health status', () => {
        it('labels a gray Offline beacon Offline', () => {
            const state = getBeaconVisualState(makeBeacon({ online: false, serverStatus: 'Offline' }));
            expect(state.isOffline).toBe(true);
            expect(state.statusLabel).toBe('Offline');
            expect(state.title).toBe('offline - click for details');
        });

        it('draws a Degraded beacon gray and labels it Degraded', () => {
            const state = getBeaconVisualState(makeBeacon({ online: false, serverStatus: 'Degraded', telemetryStale: true }));
            expect(state.isOffline).toBe(true);
            expect(state.isTelemetryStale).toBe(false);
            expect(state.color).toBe(BEACON_OFFLINE_COLOR);
            expect(state.statusLabel).toBe('Degraded');
            expect(state.title).toBe('degraded - click for details');
        });

        it('labels a gray beacon with no status yet Offline', () => {
            const state = getBeaconVisualState(makeBeacon({ online: false }));
            expect(state.statusLabel).toBe('Offline');
        });

        it('has no status label when online', () => {
            const state = getBeaconVisualState(makeBeacon({ online: true, serverStatus: 'Online' }));
            expect(state.statusLabel).toBeNull();
        });
    });

    describe('buildServerStatusPopupHtml', () => {
        it('lists each reason under the status', () => {
            const html = buildServerStatusPopupHtml('Eagle', 'Degraded', ['SDR# not running (0 of 1)', 'Low disk space']);
            expect(html).toContain('<strong>Eagle</strong>');
            expect(html).toContain('Degraded');
            expect(html).toContain('<li>SDR# not running (0 of 1)</li>');
            expect(html).toContain('<li>Low disk space</li>');
        });

        it('omits the list when there are no reasons', () => {
            const html = buildServerStatusPopupHtml('Eagle', 'Offline', []);
            expect(html).not.toContain('<ul');
        });

        it('escapes HTML in the name and reasons', () => {
            const html = buildServerStatusPopupHtml('<b>x</b>', 'Offline', ['<script>']);
            expect(html).not.toContain('<b>x</b>');
            expect(html).not.toContain('<script>');
            expect(html).toContain('&lt;script&gt;');
        });
    });

    describe('telemetry-stale beacon (blue ring)', () => {
        it('is telemetry stale when online and telemetryStale is true', () => {
            const state = getBeaconVisualState(makeBeacon({ online: true, telemetryStale: true }));
            expect(state.isOffline).toBe(false);
            expect(state.isTelemetryStale).toBe(true);
        });

        it('uses blue outline color', () => {
            const state = getBeaconVisualState(makeBeacon({ online: true, telemetryStale: true }));
            expect(state.color).toBe('#005aa9');
        });

        it('uses dark theme center fill in dark map mode', () => {
            const state = getBeaconVisualState(makeBeacon({ online: true, telemetryStale: true }), 'dark');
            expect(state.dotCenterColor).toBe('#1a1a2e');
        });

        it('uses white center fill in light map mode', () => {
            const state = getBeaconVisualState(makeBeacon({ online: true, telemetryStale: true }), 'light');
            expect(state.dotCenterColor).toBe('#ffffff');
        });

        it('reports title as telemetry stale', () => {
            const state = getBeaconVisualState(makeBeacon({ online: true, telemetryStale: true }));
            expect(state.title).toBe('telemetry stale');
        });
    });

    describe('theme-aware center fill only applies to telemetry-stale state', () => {
        it('does not change dotCenterColor for healthy beacon in light mode', () => {
            const state = getBeaconVisualState(makeBeacon({ online: true, telemetryStale: false }), 'light');
            expect(state.dotCenterColor).toBe('#005aa9');
        });

        it('does not change dotCenterColor for offline beacon in light mode', () => {
            const state = getBeaconVisualState(makeBeacon({ online: false }), 'light');
            expect(state.dotCenterColor).toBe('#888888');
        });
    });
});
