import React from 'react';
// import { Popup } from 'react-leaflet';
import { Beacon } from '../types/Beacon';
import BeaconMarker from './BeaconMarker';
import BeaconLabelPin from './BeaconLabelPin';

interface BeaconMarkersProps {
    pins: Beacon[];
    zoom: number;
    mapTheme: 'dark' | 'light';
    beaconLastUpdateMap?: { [beaconID: string]: { lastUpdate: string, direction: string | null } };
}

const BeaconMarkers: React.FC<BeaconMarkersProps> = ({ pins: beaconPins, zoom, mapTheme, beaconLastUpdateMap }) => {
    // Vertical offset for label marker (in degrees latitude, approx 7px)
    const getLabelOffsetLat = (lat: number, zoom: number) => {
        // Pointer is 1/3 smaller, so 7px at current zoom
        const metersPerPixel = 156543.03392 * Math.cos(lat * Math.PI / 180) / Math.pow(2, zoom);
        const offsetMeters = 7 * metersPerPixel;
        return lat - (offsetMeters / 111320); // 1 deg lat ~ 111.32km
    };

    return (
        <>
            {beaconPins.map((beaconPin, idx) => {
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
                return (
                    <React.Fragment key={`beacon-group-${beaconPin.beaconID ?? idx}`}>
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
                            />
                        )}
                    </React.Fragment>
                );
            })}
        </>
    );
};

export default BeaconMarkers;
