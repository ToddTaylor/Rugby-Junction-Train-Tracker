import React from 'react';
import L from 'leaflet';
import { Marker } from 'react-leaflet';
import { Beacon } from '../types/Beacon';
import { MapPin } from '../types/MapPin';
import { TrackedPin } from '../services/trackedPins';

interface BeaconLabelPinProps {
    beaconPin: Beacon;
    idx: number;
    zoom: number;
    mapTheme: 'dark' | 'light';
    getLabelOffsetLat: (lat: number, zoom: number) => number;
    lastUpdateTime?: string | null;
    direction?: string | null;
    onClick?: (beaconID: string, beaconName: string) => void;
    trackedPins?: TrackedPin[];
    mapPins?: MapPin[];
}

const BeaconLabelPin: React.FC<BeaconLabelPinProps> = ({ 
    beaconPin, 
    idx, 
    zoom, 
    mapTheme, 
    getLabelOffsetLat, 
    lastUpdateTime, 
    direction, 
    onClick, 
    trackedPins = [], 
    mapPins = []
}) => {
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

    // Add status text below label, with direction between 'Last Train:' and timestamp
    // Map direction letter to arrow icon
    function getDirectionArrow(dir: string | null): string {
        switch (dir) {
            case 'N': return '▲';
            case 'S': return '▼';
            case 'E': return '►';
            case 'W': return '◄';
            default: return dir ? dir : '';
        }
    }

    let statusText = '';
    if (lastUpdateTime) {
        statusText = `Last Train: ${direction ? getDirectionArrow(direction) + ' ' : ''}${lastUpdateTime}`;
    }
    // Style for status text
    const statusFontSize = labelFontSize;
    const statusFontWeight = 200;
    const statusLetterSpacing = '0.5px';
    const statusFontFamily = `'Segoe UI ExtraLight', 'Segoe UI Light', 'Segoe UI', 'Arial', 'Helvetica Neue', Helvetica, Arial, sans-serif`;
    const statusTextColor = mapTheme === 'dark' ? '#fffbe6' : '#005aa9';
    const statusBg = mapTheme === 'dark' ? 'rgba(0,0,0,0.45)' : 'rgba(255,255,255,0.7)';
    const statusTextShadow = mapTheme === 'dark'
        ? '0 1px 4px #000, 0 0 2px #fffbe6'
        : '0 1px 4px #fff, 0 0 2px #005aa9';
    const statusPadding = `${labelPadding / 2}px 8px`;
    const statusRadius = `${labelRadius / 1.5}px`;
    
    // Find tracked trains at this beacon (only expired/not visible)
    const expiredTrackedTrains = trackedPins
        .map(trackedPin => {
            // Find the map pin for this tracked train
            const mapPin = mapPins.find(pin => String(pin.id) === String(trackedPin.id));
            
            // Only show indicator if pin doesn't exist but was last seen at this beacon
            if (!mapPin && trackedPin.lastBeaconID === beaconPin.beaconID) {
                return {
                    id: trackedPin.id,
                    color: trackedPin.color
                };
            }
            
            return null;
        })
        .filter(item => item !== null);
    
    const handleStatusClick = () => {
        if (onClick && beaconPin.beaconID && beaconPin.beaconName) {
            onClick(beaconPin.beaconID, beaconPin.beaconName);
        }
    };
    
    return (
        <Marker
            key={`beacon-label-${beaconPin.beaconID ?? idx}`}
            position={[getLabelOffsetLat(beaconPin.latitude, zoom), beaconPin.longitude]}
            pane="beaconPane"
            eventHandlers={{
                click: (e) => {
                    // Check if click target is the beacon name or status div
                    const target = e.originalEvent.target as HTMLElement;
                    if (target && (target.closest('.beacon-status') || target.closest('.beacon-name'))) {
                        handleStatusClick();
                        L.DomEvent.stopPropagation(e.originalEvent);
                    }
                }
            }}
            icon={L.divIcon({
                className: 'beacon-label-marker',
                html: `
                    <div style=\"position: relative; display: flex; flex-direction: column; align-items: center;\">
                        <div style=\"position: absolute; top: 0; left: 50%; transform: translateX(-50%); z-index: 0; width:0;height:0;border-left:${pointerBorderWidth}px solid transparent;border-right:${pointerBorderWidth}px solid transparent;border-bottom:${pointerBorderHeight}px solid ${borderColor};\"></div>
                        <div style=\"position: relative; z-index: 1; width:0;height:0;border-left:${pointerWidth}px solid transparent;border-right:${pointerWidth}px solid transparent;border-bottom:${pointerHeight}px solid ${pointerColor};margin-top:2px;\"></div>
                        <div class=\"beacon-name\" style=\"background:${labelBg};color:${labelColor};font-size:${labelFontSize}px;font-family:'Helvetica Neue',Helvetica,Arial,sans-serif;font-weight:500;padding:${labelPadding}px 12px;border-radius:${labelRadius}px;box-shadow:0 1px 6px rgba(0,0,0,0.13);margin-top:-2px;white-space:nowrap;text-transform:uppercase;border:1px solid ${borderColor};cursor:pointer;\">${beaconPin.beaconName || ''}</div>
                        ${statusText ? `<div class=\"beacon-status\" style=\"
                            background:${statusBg};
                            color:${statusTextColor};
                            font-size:${statusFontSize}px;
                            font-family:${statusFontFamily};
                            font-weight:${statusFontWeight};
                            letter-spacing:${statusLetterSpacing};
                            margin-top:2px;
                            white-space:nowrap;
                            text-shadow:${statusTextShadow};
                            padding:${statusPadding};
                            border-radius:${statusRadius};
                            cursor:pointer;
                        \">${statusText}</div>` : ''}
                        ${expiredTrackedTrains.length > 0 ? `<div style=\"display: flex; flex-direction: row; gap: 4px; margin-top: 4px;\">
                            ${expiredTrackedTrains.map(train => `
                                <div style=\"
                                    width: 14px;
                                    height: 14px;
                                    background-color: ${train.color};
                                    border-radius: 50%;
                                    border: 1px solid rgba(0, 0, 0, 0.5);
                                    display: flex;
                                    align-items: center;
                                    justify-content: center;
                                    font-size: 10px;
                                    font-weight: 900;
                                    color: #000;
                                    line-height: 14px;
                                \">T</div>
                            `).join('')}
                        </div>` : ''}
                    </div>
                `,
                iconSize: [iconWidth, iconHeight + (statusText ? 18 : 0) + (expiredTrackedTrains.length > 0 ? 20 : 0)],
                iconAnchor: [iconAnchorX, iconAnchorY],
            })}
        />
    );
};

export default BeaconLabelPin;
