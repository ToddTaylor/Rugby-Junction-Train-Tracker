import React, { useRef, useEffect, useState } from 'react';
import L from 'leaflet';
import { Marker } from 'react-leaflet';
import { Beacon } from '../types/Beacon';
import { TrackedPin, updateTrackedPinSymbol, removeTrackedMapPin } from '../services/trackedPins';
import { MapPin } from '../types/MapPin';
import TrackSymbolModal from './TrackSymbolModal';
import { getBeaconDotSizePx } from '../utils/markerSizing';
import { fetchBeaconLatestFromHistory } from '../services/mapPinsHistory';

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
    // Fetch most recent history entry from same API as BeaconHistoryModal to ensure timestamp accuracy
    const [fetchedLastUpdateTime, setFetchedLastUpdateTime] = useState<string | null>(null);
    const [fetchedDirection, setFetchedDirection] = useState<string | null | undefined>(undefined); // undefined = not yet fetched

    // Always update both timestamp and direction together, even if direction is null
    const updateFromHistory = async (bypassCache = false) => {
        if (!beaconPin.beaconID) return;
        try {
            const latest = await fetchBeaconLatestFromHistory(beaconPin.beaconID, beaconPin.subdivisionID, { bypassCache });
            if (latest?.lastUpdate) {
                const d = new Date(latest.lastUpdate);
                setFetchedLastUpdateTime(d.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' }));
                setFetchedDirection(Object.prototype.hasOwnProperty.call(latest, 'direction') ? latest.direction : null);
            } else {
                // If no latest record, only clear direction
                setFetchedDirection(null);
            }
        } catch (e) {
            console.error('Error fetching latest beacon history:', e);
            setFetchedDirection(null);
        }
    };

    useEffect(() => {
        updateFromHistory(false);
    }, [beaconPin.beaconID, beaconPin.subdivisionID]);

    // On live map pin changes (e.g., SignalR updates), update immediately from the latest mapPins data
    // Do NOT fetch from history API here as it creates a race condition causing flickering
    // Only update timestamp from live data, NOT direction - always use verified history data for direction
    useEffect(() => {
        if (!beaconPin.beaconID || !mapPins.length) return;
        const latestForBeacon = mapPins
            .filter(pin => String(pin.beaconID || '') === String(beaconPin.beaconID || '')
                && String(pin.subdivisionID || '') === String(beaconPin.subdivisionID || ''))
            .reduce<{ lastUpdate: string | null; direction: string | null } | null>((acc, pin) => {
                if (!pin.lastUpdate) return acc;
                if (!acc || new Date(pin.lastUpdate) > new Date(acc.lastUpdate || 0)) {
                    return { lastUpdate: pin.lastUpdate, direction: pin.direction ?? null };
                }
                return acc;
            }, null);

        if (latestForBeacon?.lastUpdate) {
            const d = new Date(latestForBeacon.lastUpdate);
            setFetchedLastUpdateTime(d.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' }));
            // Never override direction from history with live data - only history is authoritative
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [mapPins, beaconPin.beaconID, beaconPin.subdivisionID]);

    // Use fetched data if available, otherwise fall back to props
    // For direction: only use fallback prop if history hasn't been fetched yet
    const actualLastUpdateTime = fetchedLastUpdateTime || lastUpdateTime;
    const actualDirection = fetchedDirection !== undefined ? fetchedDirection : direction;

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
    // iconAnchorY will be calculated dynamically in useMemo to account for statusText
    const labelBg = mapTheme === 'dark' ? '#222' : '#fff';
    const labelColor = mapTheme === 'dark' ? '#eaf3ff' : '#005aa9';
    const pointerColor = mapTheme === 'dark' ? '#222' : '#fff';
    const borderColor = mapTheme === 'dark' ? '#444' : '#c5d8ee';
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

    // Map direction letter to arrow icon

    function getDirectionArrowSvg(dir: string | null, size = 16, color = statusTextColor): string {
        if (!dir) return '';
        let rotate = 0;
        switch (dir) {
            case 'N': rotate = 0; break;
            case 'S': rotate = 180; break;
            case 'E': rotate = 90; break;
            case 'W': rotate = -90; break;
            case 'NE': rotate = 45; break;
            case 'SE': rotate = 135; break;
            case 'NW': rotate = -45; break;
            case 'SW': rotate = -135; break;
            default: return '';
        }
        return `<span style="display:inline-block;align-self:center;transform:rotate(${rotate}deg);"><svg width="${size}" height="${size}" viewBox="0 0 16 16"><polygon points="8,2 14,14 2,14" fill="${color}" /></svg></span>`;
    }

    let statusText = '';
    if (actualLastUpdateTime) {
        statusText = `<span style="display:inline-flex;align-items:center;gap:4px;">Last Train: ${actualDirection ? getDirectionArrowSvg(actualDirection, 16, statusTextColor) : ''} ${actualLastUpdateTime}</span>`;
    } else {
        statusText = '<span style="display:inline-flex;align-items:center;">Last Train: N/A</span>';
    }

    // For single beacon labels, show the same statusText (including N/A when no data)
    const statusTextForSingleBeacon = statusText;
    
    // ...existing code...
    
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
    
    const handleSaveSymbol = async (newSymbol: string) => {
        if (selectedTrainId) {
            try {
                if (newSymbol) {
                    await updateTrackedPinSymbol(selectedTrainId, newSymbol);
                } else {
                    await updateTrackedPinSymbol(selectedTrainId, '');
                }
                // Optionally update parent state if needed
            } catch (error) {
                console.error('Failed to save symbol:', error);
                alert(`Failed to save symbol: ${error instanceof Error ? error.message : 'Unknown error'}`);
                throw error; // Re-throw to keep modal open
            }
        }
    };

    const handleUntrackTrain = async () => {
        if (selectedTrainId) {
            try {
                await removeTrackedMapPin(selectedTrainId);
                // Optionally update parent state if needed
            } catch (error) {
                console.error('Failed to untrack train:', error);
                alert(`Failed to untrack train: ${error instanceof Error ? error.message : 'Unknown error'}`);
                throw error; // Re-throw to keep modal open
            }
        }
    };

    // ==================================================================================
    // BEACON LABEL CONFIGURATION - DO NOT MODIFY WITHOUT CAREFUL TESTING
    // ==================================================================================
    // showArrow: true = MULTIPLE BEACONS at same location (callout style with diagonal triangle)
    // showArrow: false = SINGLE BEACON (centered label with downward-pointing triangle above)
    // ==================================================================================
    const showArrow = horizontalShift !== 0;
    const arrowOnRight = horizontalShift < 0;
    const actualLabelHeight = (labelFontSize + labelPadding * 2 + 2);
    const beaconDotRadius = getBeaconDotSizePx(zoom) / 2;
    
    // Triangle dimensions for multi-beacon callout style
    const triangleWidth = 20 * base;
    const triangleHeight = beaconDotRadius + 4;
    
    const markerIcon = React.useMemo(() => L.divIcon({
                className: 'beacon-label-marker',
                // ==================================================================================
                // MULTIPLE BEACON LABEL FORMAT (showArrow = true)
                // ==================================================================================
                // - Label positioned BELOW and to the LEFT or RIGHT of beacon
                // - Diagonal triangle extends from top corner of label UP to beacon center
                // - arrowOnRight: true = label on LEFT side, triangle points up-right to beacon
                // - arrowOnRight: false = label on RIGHT side, triangle points up-left to beacon
                // - "Last Train" status text shown below label
                // - DO NOT CHANGE THIS FORMAT - it was carefully designed to prevent label overlap
                // ==================================================================================
                html: showArrow ? `
                    <div style="position: absolute; ${arrowOnRight ? 'right' : 'left'}: 0; top: ${beaconDotRadius * 2}px; display: flex; flex-direction: column; align-items: ${arrowOnRight ? 'flex-end' : 'flex-start'}; pointer-events: none; overflow: visible;">
                        <div class="beacon-name" style="position: relative; cursor: pointer; pointer-events: auto;">
                            <svg style="position: absolute; ${arrowOnRight ? 'right' : 'left'}: -1px; top: -${triangleHeight - 1}px; width: ${triangleWidth + 2}px; height: ${triangleHeight + 1}px; overflow: visible; z-index: 1;" viewBox="0 0 ${triangleWidth} ${triangleHeight}">
                                <polygon points="${arrowOnRight 
                                    ? `${triangleWidth},0 ${triangleWidth},${triangleHeight} 0,${triangleHeight}` 
                                    : `0,0 ${triangleWidth},${triangleHeight} 0,${triangleHeight}`}" 
                                    fill="${borderColor}"/>
                                <polygon points="${arrowOnRight 
                                    ? `${triangleWidth - 1},1 ${triangleWidth - 1},${triangleHeight} 1,${triangleHeight}` 
                                    : `1,1 ${triangleWidth - 1},${triangleHeight} 1,${triangleHeight}`}" 
                                    fill="${labelBg}"/>
                            </svg>
                            <div style="
                                position: relative;
                                top: 0;
                                display: inline-flex;
                                align-items: center;
                                background: ${labelBg};
                                color: ${labelColor};
                                font-size: ${labelFontSize}px;
                                font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif;
                                font-weight: 500;
                                padding: ${labelPadding}px 8px;
                                white-space: nowrap;
                                text-transform: uppercase;
                                border-radius: ${labelRadius}px;
                                border: 1px solid ${borderColor};
                                border-top-${arrowOnRight ? 'right' : 'left'}-radius: 0;
                                box-shadow: 0 1px 6px rgba(0,0,0,0.13);
                            ">
                                ${beaconPin.beaconName || ''}
                                ${beaconPin.railroad ? `<span style="position: absolute; bottom: -5px; right: 4px; font-size: 9px; font-weight: 400; background: #005aa9; color: #fff; padding: 0px 3px; border-radius: 3px; white-space: nowrap; line-height: 1.2;">${beaconPin.railroad.toUpperCase()}</span>` : ''}
                            </div>
                        </div>
                        ${statusText ? `<div class="beacon-status" style="
                            display: inline-block;
                            margin-top: 4px;
                            background:${statusBg};
                            color:${statusTextColor};
                            font-size:${statusFontSize}px;
                            font-family:${statusFontFamily};
                            font-weight:${statusFontWeight};
                            letter-spacing:${statusLetterSpacing};
                            white-space:nowrap;
                            text-shadow:${statusTextShadow};
                            padding:${statusPadding};
                            border-radius:${statusRadius};
                            cursor:pointer;
                            pointer-events:auto;
                        ">${statusText}</div>` : ''}
                        ${trackedTrainsAtBeacon.length > 0 ? `<div style="display: flex; flex-direction: column; gap: 2px; align-items: ${arrowOnRight ? 'flex-end' : 'flex-start'}; margin-top: 4px;">
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
                ` : `
                    <!-- ==================================================================================
                         SINGLE BEACON LABEL FORMAT (showArrow = false)
                         ==================================================================================
                         - Label centered directly above beacon dot
                         - Small downward-pointing triangle connects label to beacon
                         - "Last Train" status text shown below label (only if available)
                         - This is the default format when only one beacon at a location
                         - DO NOT CHANGE THIS FORMAT - it is the standard beacon label appearance
                         ================================================================================== -->
                    <div style="position: relative; display: flex; flex-direction: column; align-items: center; pointer-events: none;">
                        <div style="position: absolute; top: 0; left: 50%; transform: translateX(-50%); z-index: 0; width:0;height:0;border-left:${pointerBorderWidth}px solid transparent;border-right:${pointerBorderWidth}px solid transparent;border-bottom:${pointerBorderHeight}px solid ${borderColor};"></div>
                        <div style="position: relative; z-index: 1; width:0;height:0;border-left:${pointerWidth}px solid transparent;border-right:${pointerWidth}px solid transparent;border-bottom:${pointerHeight}px solid ${pointerColor};margin-top:2px;"></div>
                        <div style="display: flex; flex-direction: column; align-items: center;">
                            <div class="beacon-name" style="position: relative; background:${labelBg};color:${labelColor};font-size:${labelFontSize}px;font-family:'Helvetica Neue',Helvetica,Arial,sans-serif;font-weight:500;padding:${labelPadding}px 12px;border-radius:${labelRadius}px;box-shadow:0 1px 6px rgba(0,0,0,0.13);margin-top:-2px;white-space:nowrap;text-transform:uppercase;border:1px solid ${borderColor};cursor:pointer;pointer-events:auto;">
                                ${beaconPin.beaconName || ''}
                                ${beaconPin.railroad ? `<span style="position: absolute; bottom: -5px; right: 4px; font-size: 9px; font-weight: 400; background: #005aa9; color: #fff; padding: 0px 3px; border-radius: 3px; white-space: nowrap; line-height: 1.2;">${beaconPin.railroad.toUpperCase()}</span>` : ''}
                            </div>
                            ${statusTextForSingleBeacon ? `<div class="beacon-status" style="
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
                            ">${statusTextForSingleBeacon}</div>` : ''}
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
                iconSize: showArrow 
                    ? [1, 1]
                    : [iconWidth, iconHeight + 18 + (trackedTrainsAtBeacon.length > 0 ? 20 : 0)],
                iconAnchor: showArrow
                    ? [0, 0]
                    : [iconAnchorX, 0],
            }), [
                beaconPin.beaconID,
                beaconPin.beaconName, 
                beaconPin.railroad, 
                beaconPin.subdivisionID,
                statusText,
                statusTextForSingleBeacon,
                trackedTrainsAtBeacon,
                labelBg, 
                labelColor, 
                pointerColor, 
                borderColor,
                showArrow,
                arrowOnRight,
                actualLabelHeight,
                beaconDotRadius,
                labelFontSize,
                labelPadding,
                labelRadius,
                pointerWidth,
                pointerHeight,
                pointerBorderWidth,
                pointerBorderHeight,
                statusFontSize,
                statusFontWeight,
                statusLetterSpacing,
                statusFontFamily,
                statusTextColor,
                statusBg,
                statusTextShadow,
                statusPadding,
                statusRadius,
                iconWidth,
                iconHeight,
                iconAnchorX,
                base
            ]);

    return (
        <>
            <Marker
            ref={markerRef}
            key={`beacon-label-${beaconPin.beaconID ?? idx}`}
            position={showArrow 
                ? [beaconPin.latitude, beaconPin.longitude] 
                : [getLabelOffsetLat(beaconPin.latitude, zoom), beaconPin.longitude]}
            pane="beaconLabelPane"
            icon={markerIcon}
        />
        <TrackSymbolModal
            key={selectedTrainId || selectedMapPinId}
            open={modalOpen}
            currentSymbol={selectedSymbol}
            onSave={handleSaveSymbol}
            onUntrack={handleUntrackTrain}
            onClose={() => {
                setModalOpen(false);
                setSelectedSymbol('');
                setSelectedTrainId(null);
                setSelectedMapPinId(undefined);
                setSelectedAddresses([]);
            }}
            theme={mapTheme}
            trainId={selectedTrainId}
            mapPinId={selectedMapPinId}
            addresses={selectedAddresses}
        />
        </>
    );
};

export default BeaconLabelPin;
