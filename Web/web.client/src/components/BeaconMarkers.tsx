import React from 'react';
import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { Beacon } from '../types/Beacon';
import { metersToPixels } from '../utils/geo';
import { getBeaconDotSizePx } from '../utils/markerSizing';

interface BeaconMarkersProps {
    pins: Beacon[];
    zoom: number;
}

// Adjustable real-world diameter for the outline (in meters)
const OUTLINE_DIAMETER_METERS = 7080;
const MIN_OUTLINE_PX = 8; // allow smaller for high zoom
const MAX_OUTLINE_PX = 2048; // allow very large for low zoom

const BeaconMarkers: React.FC<BeaconMarkersProps> = ({ pins: beaconPins, zoom }) => {
    return (
        <>
            {beaconPins.map((beaconPin, idx) => {
                const color = beaconPin.online === false ? '#888888' : '#005aa9';
                const title = beaconPin.online === true ? 'online' : 'offline';
                const beaconDotSizePx = getBeaconDotSizePx(zoom);

                // Use metersToPixels for correct scaling
                let outlineSize = metersToPixels(OUTLINE_DIAMETER_METERS, beaconPin.latitude, zoom);
                outlineSize = Math.max(MIN_OUTLINE_PX, Math.min(MAX_OUTLINE_PX, outlineSize));

                // Center the beacon dot marker
                const beaconDotOffsetPx = (outlineSize - beaconDotSizePx) / 2;

                // Ping base size so that at scale(10) it matches outlineSize
                const pingBaseSizePx = outlineSize / 10;

                const dottedOutline = beaconPin.online === true
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
                    "></div>`
                    : '';

                // Ping is absolutely centered and animates to match outline
                const pingDiv = beaconPin.online !== false
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
                                <div class=\"beacon-container\" style=\"position: relative; width: ${outlineSize}px; height: ${outlineSize}px;\">
                                    ${dottedOutline}
                                    <div class=\"beacon-dot\" title=\"Beacon ${title}\" style=\"
                                        width:${beaconDotSizePx}px;
                                        height:${beaconDotSizePx}px;
                                        background:${color};
                                        border-radius:50%;
                                        position:absolute;
                                        top:${beaconDotOffsetPx}px;
                                        left:${beaconDotOffsetPx}px;
                                        z-index:2;
                                    \" ></div>
                                    ${pingDiv}
                                </div>
                            `,
                            iconSize: [outlineSize, outlineSize],
                            iconAnchor: [outlineSize / 2, outlineSize / 2],
                        })}
                    />
                );
            })}
        </>
    );
};

export default BeaconMarkers;
