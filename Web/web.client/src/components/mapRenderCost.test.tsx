import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { MapContainer, useMap } from 'react-leaflet';
import L from 'leaflet';
import { afterEach, beforeEach, describe, expect, it, vi, type MockInstance } from 'vitest';
import TelemetryMarkers from './TelemetryMarkers';
import BeaconMarkers from './BeaconMarkers';
import type { MapPin } from '../types/MapPin';
import type { Beacon } from '../types/Beacon';
import type { TrackedPin } from '../services/trackedPins';

// These tests count L.divIcon calls. A new icon makes Leaflet replace the marker's DOM element, so each call is
// a marker redrawn. Before this change every map render redrew every train and every beacon label.

const NOW = new Date('2026-11-16T15:00:00Z');

function pin(id: string, overrides: Partial<MapPin> = {}): MapPin {
  return {
    id,
    beaconID: '1',
    beaconName: 'Rugby',
    latitude: 44.5,
    longitude: -89.5 - Number(id) / 100,
    milepost: 100,
    direction: 'N',
    moving: true,
    isLocal: false,
    railroad: 'CN',
    subdivision: 'Neenah',
    subdivisionID: '7',
    lastUpdate: NOW.toISOString(),
    addresses: [{ addressID: Number(id), source: 'EOT', isActive: true }],
    ...overrides,
  } as MapPin;
}

function beacon(id: string): Beacon {
  return {
    beaconID: id,
    beaconName: `Beacon ${id}`,
    railroadID: '1',
    railroad: 'CN',
    subdivisionID: '7',
    subdivision: 'Neenah',
    milepost: 100,
    latitude: 44.5 + Number(id) / 10,
    longitude: -89.5,
    online: true,
  };
}

/** Creates the panes RailMap sets up, which the markers draw into. */
function Panes() {
  const map = useMap();
  for (const name of ['beaconPane', 'beaconLabelPane', 'telemetryPane']) {
    if (!map.getPane(name)) map.createPane(name);
  }
  return null;
}

/** Rebuilds each pin as a new object, the way RailMap does whenever any pin changes. */
function asMapState(pins: MapPin[]): { [id: string]: MapPin } {
  return Object.fromEntries(pins.map(p => [p.id, { ...p }]));
}

describe('map marker render cost', () => {
  let container: HTMLDivElement;
  let root: Root;
  let divIcon: MockInstance<typeof L.divIcon>;

  function render(children: React.ReactNode) {
    act(() => root.render(
      <MapContainer center={[44.5, -89.5]} zoom={12} style={{ height: 400, width: 400 }}>
        <Panes />
        {children}
      </MapContainer>
    ));
  }

  beforeEach(() => {
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(NOW);
    window.matchMedia ??= ((query: string) => ({
      matches: false, media: query, onchange: null,
      addListener: () => {}, removeListener: () => {}, addEventListener: () => {}, removeEventListener: () => {},
      dispatchEvent: () => false,
    })) as typeof window.matchMedia;
    container = document.createElement('div');
    document.body.appendChild(container);
    root = createRoot(container);
    divIcon = vi.spyOn(L, 'divIcon');
  });

  afterEach(() => {
    act(() => root.unmount());
    container.remove();
    vi.useRealTimers();
  });

  describe('TelemetryMarkers', () => {
    const trains = [pin('1'), pin('2'), pin('3')];

    function renderTrains(pins: { [id: string]: MapPin }, trackedPins: TrackedPin[] = [], zoom = 12) {
      render(
        <TelemetryMarkers pins={pins} zoom={zoom} maxPinAgeMinutes={60} trackedPins={trackedPins} mapTheme="dark" />
      );
    }

    it('draws each train once', () => {
      renderTrains(asMapState(trains));

      expect(divIcon).toHaveBeenCalledTimes(3);
    });

    it('redraws only the train that changed when one train updates', () => {
      renderTrains(asMapState(trains));
      divIcon.mockClear();

      renderTrains(asMapState([trains[0], { ...trains[1], direction: 'S' }, trains[2]]));

      expect(divIcon).toHaveBeenCalledTimes(1);
    });

    it('redraws nothing when the map re-renders with the same trains', () => {
      renderTrains(asMapState(trains));
      divIcon.mockClear();

      renderTrains(asMapState(trains));

      expect(divIcon).not.toHaveBeenCalled();
    });

    it('redraws only the train whose tracked pin changed', () => {
      renderTrains(asMapState(trains));
      divIcon.mockClear();

      renderTrains(asMapState(trains), [{ id: '2', color: '#ff0000', expires: NOW.getTime() + 60_000 }]);

      expect(divIcon).toHaveBeenCalledTimes(1);
    });

    it('redraws every train when the zoom changes their size', () => {
      renderTrains(asMapState(trains));
      divIcon.mockClear();

      renderTrains(asMapState(trains), [], 14);

      expect(divIcon).toHaveBeenCalledTimes(3);
    });
  });

  describe('BeaconMarkers labels', () => {
    const beacons = [beacon('1'), beacon('2')];
    type LastUpdateMap = { [key: string]: { lastUpdate: string; direction: string | null } };
    const lastUpdateMap: LastUpdateMap = { '1|7': { lastUpdate: '2026-11-16T14:05:00Z', direction: 'N' } };

    function renderBeacons(mapPins: MapPin[], hourFormat = '24', map = lastUpdateMap, onBeaconClick?: () => void) {
      render(
        <BeaconMarkers
          pins={beacons}
          onBeaconClick={onBeaconClick}
          zoom={12}
          mapTheme="dark"
          beaconLastUpdateMap={map}
          trackedPins={[]}
          mapPins={mapPins}
          hourFormat={hourFormat}
        />
      );
    }

    function labelText(): string {
      return Array.from(container.querySelectorAll('.beacon-status')).map(el => el.textContent?.trim()).join(' | ');
    }

    it('shows the last train without asking the server', () => {
      const fetchSpy = vi.spyOn(globalThis, 'fetch').mockResolvedValue(new Response('{"data":[]}'));

      renderBeacons([]);

      expect(fetchSpy).not.toHaveBeenCalled();
      const expected = new Date('2026-11-16T14:05:00Z').toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false });
      expect(labelText()).toBe(`Last Train:  ${expected} | Last Train: N/A`);
    });

    it('follows the 12-hour setting', () => {
      renderBeacons([], '12');

      const expected = new Date('2026-11-16T14:05:00Z').toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', hour12: true });
      expect(labelText()).toContain(expected);
    });

    it('does not redraw labels when a train moves and the user tracks nothing', () => {
      renderBeacons([pin('1')]);
      divIcon.mockClear();

      renderBeacons([pin('1', { lastUpdate: '2026-11-16T14:59:00Z' })]);

      expect(divIcon).not.toHaveBeenCalled();
    });

    it('redraws only the label whose last train changed', () => {
      renderBeacons([]);
      divIcon.mockClear();

      renderBeacons([], '24', { ...lastUpdateMap, '2|7': { lastUpdate: '2026-11-16T14:30:00Z', direction: 'S' } });

      expect(divIcon).toHaveBeenCalledTimes(1);
    });

    it('opens the beacon history when a label is clicked, also after its last train changed', () => {
      const onBeaconClick = vi.fn();
      renderBeacons([], '24', lastUpdateMap, onBeaconClick);
      renderBeacons([], '24', { '1|7': { lastUpdate: '2026-11-16T14:50:00Z', direction: 'S' } }, onBeaconClick);

      const name = Array.from(container.querySelectorAll('.beacon-name')).find(el => el.textContent?.includes('Beacon 1'));
      act(() => { name!.dispatchEvent(new MouseEvent('click', { bubbles: true })); });

      expect(onBeaconClick).toHaveBeenCalledTimes(1);
      expect(onBeaconClick.mock.calls[0].slice(0, 2)).toEqual(['1', 'Beacon 1']);
    });
  });
});
