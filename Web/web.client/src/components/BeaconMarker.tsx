import React from 'react';
import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { Beacon } from '../types/Beacon';
import { metersToPixels } from '../utils/geo';
import { getBeaconDotSizePx } from '../utils/markerSizing';

// Adjustable real-world diameter for the outline (in meters)
const OUTLINE_DIAMETER_METERS = 7080;
const MIN_OUTLINE_PX = 8; // allow smaller for high zoom
const MAX_OUTLINE_PX = 2048; // allow very large for low zoom

interface BeaconMarkerProps {
    pin: Beacon;
    zoom: number;
    idx: number;
    mapTheme?: 'dark' | 'light';
}

export interface BeaconVisualState {
    isOffline: boolean;
    isTelemetryStale: boolean;
    color: string;
    dotCenterColor: string;
    title: string;
}

export function getBeaconVisualState(beacon: Beacon, mapTheme: 'dark' | 'light' = 'dark'): BeaconVisualState {
    const isOffline = beacon.online === false;
    const isTelemetryStale = beacon.online !== false && beacon.telemetryStale === true;
    const color = isOffline ? '#888888' : '#005aa9';
    const dotCenterColor = isTelemetryStale
        ? (mapTheme === 'dark' ? '#1a1a2e' : '#ffffff')
        : color;
    const title = isOffline ? 'offline' : isTelemetryStale ? 'telemetry stale' : 'online';
    return { isOffline, isTelemetryStale, color, dotCenterColor, title };
}

const BeaconMarker: React.FC<BeaconMarkerProps> = ({ pin: beaconPin, zoom, idx, mapTheme = 'dark' }) => {
    const beaconName = beaconPin.beaconName;
    const { isOffline, isTelemetryStale, dotCenterColor, title } = getBeaconVisualState(beaconPin, mapTheme);

    const beaconDotSizePx = getBeaconDotSizePx(zoom);

    // Use metersToPixels for correct scaling
    let outlineSize = metersToPixels(OUTLINE_DIAMETER_METERS, beaconPin.latitude, zoom);
    outlineSize = Math.max(MIN_OUTLINE_PX, Math.min(MAX_OUTLINE_PX, outlineSize));

    // Center the beacon dot marker
    const beaconDotOffsetPx = (outlineSize - beaconDotSizePx) / 2;

    // Ping base size so that at scale(10) it matches outlineSize
    const pingBaseSizePx = outlineSize / 10;

    // Dotted outline: shown for healthy online beacons only
    const dottedOutline = !isOffline && !isTelemetryStale
        ? `<div style="
            position:absolute;
            top:0;
            left:0;
            width:${outlineSize}px;
            height:${outlineSize}px;
            border-radius:50%;
            border:2px dotted #005aa9;
            box-sizing:border-box;
            pointer-events:none;
            z-index:1;
            cursor: default;
        "></div>`
        : '';

    // Blue ring: shown for telemetry-stale beacons (solid ring, no ping)
    const telemetryStaleRing = isTelemetryStale
        ? `<div style="
            position:absolute;
            top:0;
            left:0;
            width:${outlineSize}px;
            height:${outlineSize}px;
            border-radius:50%;
            border:3px solid #005aa9;
            box-sizing:border-box;
            pointer-events:none;
            z-index:1;
            cursor: default;
        "></div>`
        : '';

    // Ping animation: shown for healthy online beacons only
    const pingDiv = !isOffline && !isTelemetryStale
        ? `<div class=\"beacon-ping\" style=\"
            position:absolute;
            top:50%;
            left:50%;
            width:${pingBaseSizePx}px;
            height:${pingBaseSizePx}px;
            border-radius:50%;
            background:rgba(0,90,169,0.15);
            pointer-events:none;
            z-index:0;
            transform: translate(-50%, -50%);
            transform-origin: center center;
            cursor: default;
        \" ></div>`
        : '';

    return (
        <Marker
            key={`beacon-${beaconPin.beaconID ?? idx}-${beaconPin.latitude}-${beaconPin.longitude}`}
            position={[beaconPin.latitude, beaconPin.longitude]}
            pane="beaconPane"
            icon={L.divIcon({
                className: 'beacon-marker-z',
                html: `
                    <div class=\"beacon-container\" style=\"position: relative; width: ${outlineSize}px; height: ${outlineSize}px; pointer-events: none;\">
                        ${dottedOutline}
                        ${telemetryStaleRing}
                        <div class=\"beacon-dot\" title=\"${beaconName} ${title}\" style=\"
                            width:${beaconDotSizePx}px;
                            height:${beaconDotSizePx}px;
                            background:${dotCenterColor};
                            border-radius:50%;
                            position:absolute;
                            top:${beaconDotOffsetPx}px;
                            left:${beaconDotOffsetPx}px;
                            z-index:2;
                            pointer-events: auto;
                            cursor: pointer;
                            ${isTelemetryStale ? `border: 2px solid #005aa9;` : ''}
                        \" ></div>
                        ${pingDiv}
                    </div>
                `,
                iconSize: [outlineSize, outlineSize],
                iconAnchor: [outlineSize / 2, outlineSize / 2],
            })}
        />
    );
};

export default BeaconMarker;
