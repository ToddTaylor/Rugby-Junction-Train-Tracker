import React, { useMemo } from 'react';
// import { Popup } from 'react-leaflet';
import { Beacon, BeaconSubdivision } from '../types/Beacon';
import { MapPin } from '../types/MapPin';
import { TrackedPin } from '../services/trackedPins';
import BeaconMarker from './BeaconMarker';
import BeaconLabelPin from './BeaconLabelPin';
import { latestBeaconUpdate } from '../utils/beaconSubdivisions';

interface BeaconMarkersProps {
    pins: Beacon[];
    zoom: number;
    mapTheme: 'dark' | 'light';
    beaconLastUpdateMap?: { [beaconID: string]: { lastUpdate: string, direction: string | null } };
    onBeaconClick?: (beaconID: string, beaconName: string, subdivisionID?: string, railroad?: string, subdivision?: string, subdivisions?: BeaconSubdivision[]) => void;
    trackedPins?: TrackedPin[];
    mapPins?: MapPin[];
    hourFormat?: string;
}

/**
 * Collapses beacon railroad rows into the markers the map should draw, gathering each
 * location's subdivisions (and their mileposts) onto the marker.
 *
 * A beacon has one row per subdivision. Two shapes arrive here:
 *   Rugby Junction - one location, two DIFFERENT railroads running parallel. The rows differ
 *     by railroadID, so both survive as separate markers and get shifted apart.
 *   Junction City  - a true junction where one railroad crosses its own tracks. Those rows
 *     share beaconID AND railroadID, so they collapse to a single marker. Their mileposts are
 *     gathered rather than discarded, so the history modal can show every subdivision through
 *     the location (issue #80). Status is merged across the rows too: the marker is online
 *     when any subdivision is, and telemetry is stale only when every online subdivision is
 *     stale. A train on either subdivision therefore keeps the one dot solid.
 *
 * Rows whose beaconID is missing or zero are dropped as invalid.
 */
export function collapseBeaconPins(beaconPins: Beacon[]): Beacon[] {
    const rowsByComposite = new Map<string | number, Beacon[]>();
    const subdivisionsByComposite = new Map<string | number, BeaconSubdivision[]>();

    for (const b of beaconPins) {
        // Treat 0 as invalid ID (likely unset/default value)
        const hasValidBeaconID = b.beaconID !== undefined && b.beaconID !== null && Number(b.beaconID) !== 0;
        const hasValidRailroadID = b.railroadID !== undefined && b.railroadID !== null && Number(b.railroadID) !== 0;

        const key = hasValidBeaconID && hasValidRailroadID
            ? `${b.beaconID}-${b.railroadID}`
            : hasValidBeaconID
                ? b.beaconID
                : null;

        if (key === null) continue;

        rowsByComposite.set(key, [...(rowsByComposite.get(key) ?? []), b]);

        const gathered = subdivisionsByComposite.get(key) ?? [];
        if (!gathered.some(s => String(s.subdivisionID) === String(b.subdivisionID))) {
            gathered.push({
                subdivisionID: b.subdivisionID,
                subdivision: b.subdivision,
                railroadID: b.railroadID,
                railroad: b.railroad,
                milepost: b.milepost
            });
        }
        subdivisionsByComposite.set(key, gathered);
    }

    return Array.from(rowsByComposite.entries()).map(([key, rows]) => {
        // The latest row supplies identity and position; status is merged across every row so
        // the marker does not depend on which subdivision a live update happened to append last.
        const beacon = rows.length > 1 ? mergeBeaconStatus(rows) : rows[rows.length - 1];
        const subdivisions = subdivisionsByComposite.get(key) ?? [];
        // Attached even for a single subdivision, so the history modal can show a milepost
        // for a beacon that has no telemetry yet.
        return subdivisions.length > 0 ? { ...beacon, subdivisions } : beacon;
    });
}

/**
 * Merges the status of several rows for one location into the last row.
 *
 * online and offlineNote describe the beacon's radio, which every row shares, but
 * telemetryStale measures train traffic per subdivision. A quiet subdivision must not mark a
 * junction stale while trains are still passing on the other one.
 */
function mergeBeaconStatus(rows: Beacon[]): Beacon {
    const online = rows.some(r => r.online !== false);
    const onlineRows = rows.filter(r => r.online !== false);
    const telemetryStale = online && onlineRows.every(r => r.telemetryStale === true);
    const offlineNote = online ? null : rows.find(r => r.offlineNote)?.offlineNote ?? null;
    return { ...rows[rows.length - 1], online, telemetryStale, offlineNote };
}

const BeaconMarkers: React.FC<BeaconMarkersProps> = ({ 
    pins: beaconPins, 
    zoom, 
    mapTheme, 
    beaconLastUpdateMap,
    onBeaconClick,
    trackedPins = [],
    mapPins = [],
    hourFormat = '24'
}) => {
    // Vertical offset for label marker (in degrees latitude, approx 7px)
    const getLabelOffsetLat = (lat: number, zoom: number) => {
        // Pointer is 1/3 smaller, so 7px at current zoom
        const metersPerPixel = 156543.03392 * Math.cos(lat * Math.PI / 180) / Math.pow(2, zoom);
        const offsetMeters = 7 * metersPerPixel;
        return lat - (offsetMeters / 111320); // 1 deg lat ~ 111.32km
    };

    const uniqueBeaconPins = useMemo(() => collapseBeaconPins(beaconPins), [beaconPins]);

    // Detect beacons that are close together horizontally and assign horizontal shifts
    const beaconHorizontalShifts = useMemo(() => {
        const shifts = new Map<string, number>();
        const LATITUDE_THRESHOLD = 0.05;

        for (let i = 0; i < uniqueBeaconPins.length; i++) {
            for (let j = i + 1; j < uniqueBeaconPins.length; j++) {
                const b1 = uniqueBeaconPins[i];
                const b2 = uniqueBeaconPins[j];

                // Check if same beacon (ID) and latitudes are close enough
                if (b1.beaconID === b2.beaconID && Math.abs(b1.latitude - b2.latitude) < LATITUDE_THRESHOLD) {
                    const id1 = `${b1.beaconID}-${b1.railroadID}`;
                    const id2 = `${b2.beaconID}-${b2.railroadID}`;

                    // Use longitude for deterministic ordering
                    if (b1.longitude < b2.longitude) {
                        // b1 is further west, b2 is further east
                        shifts.set(id1, -50);  // b1: left-most, right-aligned label (triangle points up-left)
                        shifts.set(id2, 50);   // b2: right-most, left-aligned label (triangle points up-right)
                    } else {
                        shifts.set(id1, 50);   // b1: right-most, left-aligned label (triangle points up-right)
                        shifts.set(id2, -50);  // b2: left-most, right-aligned label (triangle points up-left)
                    }
                }
            }
        }

        return shifts;
    }, [uniqueBeaconPins]);

    return (
        <>
            {uniqueBeaconPins.map((beaconPin, idx) => {
                const LABEL_ZOOM_THRESHOLD = 11;
                // Find the latest telemetry pin for this beacon
                // Always use the last known update time and direction for this beacon
                let lastUpdateTime: string | null = null;
                let direction: string | null = null;
                // Keys match makeBeaconKey in RailMap.tsx; a junction takes its latest subdivision.
                const entry = latestBeaconUpdate(beaconLastUpdateMap, beaconPin);
                if (entry) {
                    const d = new Date(entry.lastUpdate);
                    lastUpdateTime = d.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
                    direction = entry.direction;
                }
                // Stable unique key: use beaconID+railroadID for identity, fallback to idx
                const keyRoot = beaconPin.beaconID != null && beaconPin.railroadID != null 
                    ? `beacon-${beaconPin.beaconID}-${beaconPin.railroadID}` 
                    : `beacon-idx-${idx}`;
                const shiftKey = `${beaconPin.beaconID}-${beaconPin.railroadID}`;
                const horizontalShift = beaconHorizontalShifts.get(shiftKey) || 0;
                
                return (
                    <React.Fragment key={keyRoot}>
                        <BeaconMarker
                            pin={beaconPin}
                            zoom={zoom}
                            idx={idx}
                            mapTheme={mapTheme}
                        />
                        {zoom >= LABEL_ZOOM_THRESHOLD && (
                            <BeaconLabelPin
                                beaconPin={beaconPin}
                                idx={idx}
                                zoom={zoom}
                                mapTheme={mapTheme}
                                getLabelOffsetLat={getLabelOffsetLat}
                                lastUpdateTime={lastUpdateTime}
                                direction={direction}
                                onClick={onBeaconClick}
                                trackedPins={trackedPins}
                                mapPins={mapPins}
                                horizontalShift={horizontalShift}
                                hourFormat={hourFormat}
                            />
                        )}
                    </React.Fragment>
                );
            })}
        </>
    );
};

export default BeaconMarkers;
