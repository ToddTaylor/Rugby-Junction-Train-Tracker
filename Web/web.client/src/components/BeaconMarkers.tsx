import React, { useMemo } from 'react';
// import { Popup } from 'react-leaflet';
import { Beacon } from '../types/Beacon';
import { MapPin } from '../types/MapPin';
import { TrackedPin } from '../services/trackedPins';
import BeaconMarker from './BeaconMarker';
import BeaconLabelPin from './BeaconLabelPin';

interface BeaconMarkersProps {
    pins: Beacon[];
    zoom: number;
    mapTheme: 'dark' | 'light';
    beaconLastUpdateMap?: { [beaconID: string]: { lastUpdate: string, direction: string | null } };
    onBeaconClick?: (beaconID: string, beaconName: string) => void;
    trackedPins?: TrackedPin[];
    mapPins?: MapPin[];
    maxPinAgeMinutes?: number;
}

const BeaconMarkers: React.FC<BeaconMarkersProps> = ({ 
    pins: beaconPins, 
    zoom, 
    mapTheme, 
    beaconLastUpdateMap, 
    onBeaconClick,
    trackedPins = [],
    mapPins = [],
    maxPinAgeMinutes = 60
}) => {
    // Vertical offset for label marker (in degrees latitude, approx 7px)
    const getLabelOffsetLat = (lat: number, zoom: number) => {
        // Pointer is 1/3 smaller, so 7px at current zoom
        const metersPerPixel = 156543.03392 * Math.cos(lat * Math.PI / 180) / Math.pow(2, zoom);
        const offsetMeters = 7 * metersPerPixel;
        return lat - (offsetMeters / 111320); // 1 deg lat ~ 111.32km
    };

    // Deduplicate beacons by beaconID to avoid duplicate React keys when upstream data contains repeats
    const uniqueBeaconPins = useMemo(() => {
        const byId = new Map<string | number, Beacon>();
        for (const b of beaconPins) {
            if (b.beaconID !== undefined && b.beaconID !== null) {
                byId.set(b.beaconID, b); // overwrite keeps latest occurrence
            } else {
                // Use a Symbol fallback to keep truly missing IDs unique
                byId.set(Symbol('no-id') as any, b);
            }
        }
        // Map preserves insertion order with overwrites; spread to array
        return Array.from(byId.values());
    }, [beaconPins]);

    return (
        <>
            {uniqueBeaconPins.map((beaconPin, idx) => {
                const LABEL_ZOOM_THRESHOLD = 11;
                // Find the latest telemetry pin for this beacon
                // Always use the last known update time and direction for this beacon
                let lastUpdateTime: string | null = null;
                let direction: string | null = null;
                if (beaconLastUpdateMap && beaconPin.beaconID) {
                    const entry = beaconLastUpdateMap[String(beaconPin.beaconID)];
                    if (entry && entry.lastUpdate) {
                        const d = new Date(entry.lastUpdate);
                        lastUpdateTime = d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
                        direction = entry.direction;
                    }
                }
                // Stable unique key: use beaconID plus lastUpdate (if present) for identity, fallback to idx
                const keyRoot = beaconPin.beaconID != null ? `beacon-${beaconPin.beaconID}` : `beacon-idx-${idx}`;
                return (
                    <React.Fragment key={keyRoot}>
                        <BeaconMarker
                            pin={beaconPin}
                            zoom={zoom}
                            idx={idx}
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
                                maxPinAgeMinutes={maxPinAgeMinutes}
                            />
                        )}
                    </React.Fragment>
                );
            })}
        </>
    );
};

export default BeaconMarkers;
