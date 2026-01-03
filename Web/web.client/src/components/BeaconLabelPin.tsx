import React, { useRef, useEffect, useState } from 'react';
import L from 'leaflet';
import { Marker } from 'react-leaflet';
import { Beacon } from '../types/Beacon';
import { TrackedPin, updateTrackedPinSymbol, removeTrackedMapPin } from '../services/trackedPins';
import { MapPin } from '../types/MapPin';
import TrackSymbolModal from './TrackSymbolModal';

interface BeaconLabelPinProps {
    beaconPin: Beacon;
    idx: number;
    zoom: number;
    mapTheme: 'dark' | 'light';
    getLabelOffsetLat: (lat: number, zoom: number) => number;
    lastUpdateTime?: string | null;
    direction?: string | null;
    onClick?: (beaconID: string, beaconName: string, subdivisionID?: string, railroad?: string, subdivision?: string) => void;
    trackedPins?: TrackedPin[];
    mapPins?: MapPin[];
    horizontalShift?: number;
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
    mapPins = [],
    horizontalShift = 0
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
    
    // Tracked trains last seen at this beacon (whether or not the map pin is still visible)
    const trackedTrainsAtBeacon = Array.from(
        new Map(
            trackedPins
                .filter(trackedPin => 
                    String(trackedPin.lastBeaconID || '') === String(beaconPin.beaconID || '') &&
                    String(trackedPin.lastSubdivisionID || '') === String(beaconPin.subdivisionID || '')
                )
                .map(trackedPin => [
                    trackedPin.id, // Use id as the key for deduplication
                    {
                        id: trackedPin.id,
                        color: trackedPin.color,
                        symbol: trackedPin.symbol
                    }
                ])
        ).values()
    );
    
    const handleStatusClick = () => {
        if (onClick && beaconPin.beaconID && beaconPin.beaconName) {
            onClick(beaconPin.beaconID, beaconPin.beaconName, beaconPin.subdivisionID, beaconPin.railroad, beaconPin.subdivision);
        }
    };

    const markerRef = useRef<L.Marker>(null);
    const [modalOpen, setModalOpen] = useState(false);
    const [selectedTrainId, setSelectedTrainId] = useState<string | null>(null);
    const [selectedSymbol, setSelectedSymbol] = useState('');
    const [selectedMapPinId, setSelectedMapPinId] = useState<number | undefined>(undefined);
    const [selectedAddresses, setSelectedAddresses] = useState<Array<{id: string, source: string}>>([]);

    useEffect(
        () => {
            const marker = markerRef.current;
            if (!marker) return;

            const handleClick = (e: MouseEvent): void => {
            const target = e.target as HTMLElement;
            
            // Handle track indicator clicks
            const trackIndicator = target.closest('.track-indicator');
            if (trackIndicator) {
                const trainId = trackIndicator.getAttribute('data-train-id');
                const currentSymbol = trackIndicator.getAttribute('data-symbol');
                if (trainId) {
                    e.stopPropagation();
                    const trackedPin = trackedPins.find(p => p.id === trainId);
                    const mapPin = mapPins.find(mp => String(mp.id) === trainId);
                    const addresses = mapPin?.addresses?.map(addr => ({id: String(addr.addressID), source: addr.source}))
                        || (trackedPin?.addresses ? [...trackedPin.addresses] : []);
                    setSelectedTrainId(trainId);
                    setSelectedSymbol(currentSymbol || '');
                    setSelectedMapPinId(trackedPin?.mapPinId || (mapPin?.id ? Number(mapPin.id) : undefined));
                    setSelectedAddresses(addresses);
                    setModalOpen(true);
                    return;
                }
            }

            // Handle beacon name and status clicks
            if (target.closest('.beacon-status') || target.closest('.beacon-name')) {
                handleStatusClick();
                e.stopPropagation();
            }
        };

        const markerElement = marker.getElement();
        if (markerElement) {
            markerElement.addEventListener('click', handleClick);
        }

        return () => {
            if (markerElement) {
                markerElement.removeEventListener('click', handleClick);
            }
        };
    }, [trackedTrainsAtBeacon, trackedPins, onClick, beaconPin.beaconID, beaconPin.beaconName]);
    
    const handleSaveSymbol = (newSymbol: string) => {
        if (selectedTrainId) {
            if (newSymbol) {
                updateTrackedPinSymbol(selectedTrainId, newSymbol);
            } else {
                updateTrackedPinSymbol(selectedTrainId, '');
            }
        }
    };

    const handleUntrackTrain = () => {
        if (selectedTrainId) {
            removeTrackedMapPin(selectedTrainId);
        }
    };

    return (
        <>
            <Marker
            ref={markerRef}
            key={`beacon-label-${beaconPin.beaconID ?? idx}`}
            position={[getLabelOffsetLat(beaconPin.latitude, zoom), beaconPin.longitude]}
            pane="beaconLabelPane"
            icon={L.divIcon({
                className: 'beacon-label-marker',
                html: `
                    <div style="position: relative; display: flex; flex-direction: column; align-items: center; pointer-events: none;">
                        <div style="position: absolute; top: 0; left: 50%; transform: translateX(-50%); z-index: 0; width:0;height:0;border-left:${pointerBorderWidth}px solid transparent;border-right:${pointerBorderWidth}px solid transparent;border-bottom:${pointerBorderHeight}px solid ${borderColor};"></div>
                        <div style="position: relative; z-index: 1; width:0;height:0;border-left:${pointerWidth}px solid transparent;border-right:${pointerWidth}px solid transparent;border-bottom:${pointerHeight}px solid ${pointerColor};margin-top:2px;"></div>
                        <div style="transform: translateX(${horizontalShift}px); display: flex; flex-direction: column; align-items: center;">
                            <div class="beacon-name" style="background:${labelBg};color:${labelColor};font-size:${labelFontSize}px;font-family:'Helvetica Neue',Helvetica,Arial,sans-serif;font-weight:500;padding:${labelPadding}px 12px;border-radius:${labelRadius}px;box-shadow:0 1px 6px rgba(0,0,0,0.13);margin-top:-2px;white-space:nowrap;text-transform:uppercase;border:1px solid ${borderColor};cursor:pointer;pointer-events:auto;">${beaconPin.beaconName || ''}</div>
                            ${statusText ? `<div class="beacon-status" style="
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
                                pointer-events:auto;
                            ">${statusText}</div>` : ''}
                            ${trackedTrainsAtBeacon.length > 0 ? `<div style="display: flex; flex-direction: column; gap: 2px; margin-top: 4px; align-items: center;">
                                ${trackedTrainsAtBeacon.map(train => `
                                    <div class="track-indicator" data-train-id="${train.id}" data-symbol="${train.symbol || ''}" style="
                                        display: flex;
                                        align-items: center;
                                        gap: 2px;
                                        cursor: pointer;
                                        pointer-events: auto;
                                    " title="Click to edit symbol">
                                        <div style="
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
                                        ">T</div>
                                        ${train.symbol ? `<span style="
                                            font-size: 13px;
                                            font-weight: bold;
                                            color: ${train.color};
                                            text-decoration: underline;
                                        ">${train.symbol}</span>` : ''}
                                    </div>
                                `).join('')}
                            </div>` : ''}
                        </div>
                    </div>
                `,
                iconSize: [iconWidth, iconHeight + (statusText ? 18 : 0) + (trackedTrainsAtBeacon.length > 0 ? 20 : 0)],
                iconAnchor: [iconAnchorX, iconAnchorY],
            })}
        />
        <TrackSymbolModal
            open={modalOpen}
            currentSymbol={selectedSymbol}
            onSave={handleSaveSymbol}
            onUntrack={handleUntrackTrain}
            onClose={() => setModalOpen(false)}
            theme={mapTheme}
            trainId={selectedTrainId}
            mapPinId={selectedMapPinId}
            addresses={selectedAddresses}
        />
        </>
    );
};

export default BeaconLabelPin;
