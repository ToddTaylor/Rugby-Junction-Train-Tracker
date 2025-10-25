import React from 'react';
import L from 'leaflet';
import { Marker } from 'react-leaflet';
import { Beacon } from '../types/Beacon';

interface BeaconLabelPinProps {
    beaconPin: Beacon;
    idx: number;
    zoom: number;
    mapTheme: 'dark' | 'light';
    getLabelOffsetLat: (lat: number, zoom: number) => number;
}

const BeaconLabelPin: React.FC<BeaconLabelPinProps> = ({ beaconPin, idx, zoom, mapTheme, getLabelOffsetLat }) => {
    // Sizing and style logic
    const base = 1 + (zoom - 7) * 0.09;
    const labelFontSize = 13;
    const labelPadding = 2 * base;
    const labelRadius = 8 * base;
    const pointerWidth = 7 * base;
    const pointerHeight = 9 * base;
    const pointerBorderWidth = pointerWidth + 2;
    const pointerBorderHeight = pointerHeight + 2;
    const iconWidth = 90 * base;
    const iconHeight = 38 * base;
    const iconAnchorX = iconWidth / 2;
    const iconAnchorY = 0;
    const labelBg = mapTheme === 'dark' ? '#222' : '#fff';
    const labelColor = mapTheme === 'dark' ? '#eaf3ff' : '#005aa9';
    const pointerColor = mapTheme === 'dark' ? '#222' : '#fff';
    const borderColor = mapTheme === 'dark' ? '#444' : '#c5d8ee';

    return (
        <Marker
            key={`beacon-label-${beaconPin.beaconID ?? idx}`}
            position={[getLabelOffsetLat(beaconPin.latitude, zoom), beaconPin.longitude]}
            pane="beaconPane"
            icon={L.divIcon({
                className: 'beacon-label-marker',
                html: `
                    <div style=\"position: relative; display: flex; flex-direction: column; align-items: center; cursor: grab;\">
                        <div style=\"position: absolute; top: 0; left: 50%; transform: translateX(-50%); z-index: 0; width:0;height:0;border-left:${pointerBorderWidth}px solid transparent;border-right:${pointerBorderWidth}px solid transparent;border-bottom:${pointerBorderHeight}px solid ${borderColor};\"></div>
                        <div style=\"position: relative; z-index: 1; width:0;height:0;border-left:${pointerWidth}px solid transparent;border-right:${pointerWidth}px solid transparent;border-bottom:${pointerHeight}px solid ${pointerColor};margin-top:2px;\"></div>
                        <div style=\"background:${labelBg};color:${labelColor};font-size:${labelFontSize}px;font-family:'Helvetica Neue',Helvetica,Arial,sans-serif;font-weight:500;padding:${labelPadding}px 12px;border-radius:${labelRadius}px;box-shadow:0 1px 6px rgba(0,0,0,0.13);margin-top:-2px;white-space:nowrap;text-transform:uppercase;border:1px solid ${borderColor};cursor:grab;\">${beaconPin.beaconName || ''}</div>
                    </div>
                `,
                iconSize: [iconWidth, iconHeight],
                iconAnchor: [iconAnchorX, iconAnchorY],
            })}
        />
    );
};

export default BeaconLabelPin;
