import React from 'react';
import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { Beacon } from '../types/types';

interface BeaconMarkersProps {
    pins: Beacon[];
    zoom: number;
}

function getMarkerSize(zoom: number): number {
    // Base size at zoom 11 is 10px, scale up/down with zoom
    // Clamp between 6px and 30px for usability
    return Math.max(6, Math.min(30, 10 + (zoom - 11) * 2));
}

const BeaconMarkers: React.FC<BeaconMarkersProps> = ({ pins: beaconPins, zoom }) => {
    const size = getMarkerSize(zoom);
    return (
        <>
            {beaconPins.map((beaconPin, idx) => (
                <Marker
                    key={`beacon-${beaconPin.beaconID || idx}`}
                    position={[beaconPin.latitude, beaconPin.longitude]}
                    icon={L.divIcon({
                        className: 'beacon-marker-z',
                        html: `<div style="
                            width:${size}px;
                            height:${size}px;
                            background:#005aa9;
                            border-radius:50%;
                            box-sizing:border-box;
                        "></div>`,
                        iconSize: [size, size],
                        iconAnchor: [size / 2, size / 2],
                    })}
                />
            ))}
        </>
    );
};

export default BeaconMarkers;
