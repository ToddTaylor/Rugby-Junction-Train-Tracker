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

function metersToPixels(meters: number, latitude: number, zoom: number): number {
    // Earth's circumference at equator: 40075016.686 meters
    // 256 px = 40075016.686 meters at zoom 0
    const metersPerPixel = (40075016.686 * Math.cos(latitude * Math.PI / 180)) / Math.pow(2, zoom + 8);
    return meters / metersPerPixel;
}

const REAL_WORLD_DIAMETER_METERS = 7000; // Note this is the diameter, not radius.

const BeaconMarkers: React.FC<BeaconMarkersProps> = ({ pins: beaconPins, zoom }) => {
    return (
        <>
            {beaconPins.map((beaconPin, idx) => {
                const color = beaconPin.online === false ? '#888888' : '#005aa9';
                const title = beaconPin.online === true ? 'online' : 'offline';
                const size = getMarkerSize(zoom);

                // Calculate pixel diameter for 4500 meters at this zoom/lat
                const realWorldDiameterPx = metersToPixels(REAL_WORLD_DIAMETER_METERS, beaconPin.latitude, zoom);
                const dotOffset = (realWorldDiameterPx - size) / 2;

                // Set ping base size so that at scale(10) it matches realWorldDiameterPx
                const pingBaseSize = realWorldDiameterPx / 10;

                const pingDiv = beaconPin.online !== false
                    ? `<div class="beacon-ping" style="width:${pingBaseSize}px;height:${pingBaseSize}px;"></div>`
                    : '';
                const dottedOutline = beaconPin.online === true
                    ? `<div style="
                        position:absolute;
                        top:0;
                        left:0;
                        width:${realWorldDiameterPx}px;
                        height:${realWorldDiameterPx}px;
                        border-radius:50%;
                        border:2px dotted #005aa9;
                        box-sizing:border-box;
                        pointer-events:none;
                        z-index:1;
                    "></div>`
                    : '';
                return (
                    <Marker
                        key={`beacon-${beaconPin.beaconID ?? idx}-${beaconPin.latitude}-${beaconPin.longitude}`}
                        position={[beaconPin.latitude, beaconPin.longitude]}
                        pane="beaconPane"
                        icon={L.divIcon({
                            className: 'beacon-marker-z',
                            html: `
                                <div class="beacon-container" style="position: relative; width: ${realWorldDiameterPx}px; height: ${realWorldDiameterPx}px;">
                                    ${dottedOutline}
                                    <div class="beacon-core" title="Beacon ${title}" style="
                                        width:${size}px;
                                        height:${size}px;
                                        background:${color};
                                        border-radius:50%;
                                        position:absolute;
                                        top:${dotOffset}px;
                                        left:${dotOffset}px;
                                        z-index:2;
                                    "></div>
                                    ${pingDiv}
                                </div>
                            `,
                            iconSize: [realWorldDiameterPx, realWorldDiameterPx],
                            iconAnchor: [realWorldDiameterPx / 2, realWorldDiameterPx / 2],
                        })}
                    />
                );
            })}
        </>
    );
};

export default BeaconMarkers;
