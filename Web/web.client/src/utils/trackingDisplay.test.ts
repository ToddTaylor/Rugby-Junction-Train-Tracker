import { describe, expect, it } from 'vitest';
import { buildTrackingLookup, sameTrackedPins } from './trackingDisplay';
import { propsEqualWithShallowPin } from './shallowEqual';
import type { MapPin } from '../types/MapPin';
import type { TrackedPin } from '../services/trackedPins';

const tracked = (id: string, extra: Partial<TrackedPin> = {}): TrackedPin => ({ id, color: '#f00', expires: 1, ...extra });
const mapPin = (id: string, shareCode?: string) => ({ id, shareCode }) as MapPin;

describe('buildTrackingLookup', () => {
  it('marks a train tracked, with its color and symbol, when a tracked pin has its ID', () => {
    const lookup = buildTrackingLookup([tracked('5', { color: '#0f0', symbol: 'M342' })]);

    expect(lookup(mapPin('5'))).toEqual({ isTracked: true, trackColor: '#0f0', trackedSymbol: 'M342' });
  });

  it('takes only the symbol from a tracked pin matched by share code', () => {
    const lookup = buildTrackingLookup([tracked('9', { shareCode: 'ABC', symbol: 'Q119' })]);

    expect(lookup(mapPin('5', 'ABC'))).toEqual({ isTracked: false, trackColor: undefined, trackedSymbol: 'Q119' });
  });

  it('uses the first tracked pin when two share an ID', () => {
    const lookup = buildTrackingLookup([tracked('5', { color: '#111' }), tracked('5', { color: '#222' })]);

    expect(lookup(mapPin('5')).trackColor).toBe('#111');
  });

  it('reports an untracked train as not tracked', () => {
    expect(buildTrackingLookup([])(mapPin('5'))).toEqual({ isTracked: false, trackColor: undefined, trackedSymbol: undefined });
  });
});

describe('sameTrackedPins', () => {
  it('treats lists with the same data as the same', () => {
    expect(sameTrackedPins([tracked('1')], [tracked('1')])).toBe(true);
  });

  it('notices a changed field', () => {
    expect(sameTrackedPins([tracked('1')], [tracked('1', { symbol: 'X' })])).toBe(false);
  });
});

describe('propsEqualWithShallowPin', () => {
  const addresses = [{ addressID: 1, source: 'EOT' }];

  it('treats a rebuilt pin with the same fields as equal', () => {
    expect(propsEqualWithShallowPin({ pin: { id: '1', addresses }, size: 20 }, { pin: { id: '1', addresses }, size: 20 })).toBe(true);
  });

  it('notices a changed pin field', () => {
    expect(propsEqualWithShallowPin({ pin: { id: '1', direction: 'N' } }, { pin: { id: '1', direction: 'S' } })).toBe(false);
  });

  it('notices a changed prop other than the pin', () => {
    expect(propsEqualWithShallowPin({ pin: { id: '1' }, size: 20 }, { pin: { id: '1' }, size: 22 })).toBe(false);
  });
});
