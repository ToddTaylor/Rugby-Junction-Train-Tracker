import React, { useRef, useEffect, useMemo } from 'react';
import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { Beacon } from '../types/Beacon';
import { metersToPixels } from '../utils/geo';
import { getBeaconDotSizePx } from '../utils/markerSizing';
import { BEACON_ONLINE_COLOR, BEACON_OFFLINE_COLOR } from '../constants/beaconColors';

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

function escapeHtml(value: string): string {
    return value
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

export interface BeaconVisualState {
    isOffline: boolean;
    isTelemetryStale: boolean;
    color: string;
    dotCenterColor: string;
    title: string;
    hasOfflineNote: boolean;
}

export function getBeaconVisualState(beacon: Beacon, mapTheme: 'dark' | 'light' = 'dark'): BeaconVisualState {
    const isOffline = beacon.online === false;
    const isTelemetryStale = beacon.online !== false && beacon.telemetryStale === true;
    const hasOfflineNote = isOffline && !!beacon.offlineNote;
    const color = isOffline ? BEACON_OFFLINE_COLOR : BEACON_ONLINE_COLOR;
    const dotCenterColor = isTelemetryStale
        ? (mapTheme === 'dark' ? '#1a1a2e' : '#ffffff')
        : color;
    const title = isOffline
        ? (hasOfflineNote ? 'offline - click for note' : 'offline')
        : isTelemetryStale ? 'telemetry stale' : 'online';
    return { isOffline, isTelemetryStale, color, dotCenterColor, title, hasOfflineNote };
}

const BeaconMarker: React.FC<BeaconMarkerProps> = ({ pin: beaconPin, zoom, idx, mapTheme = 'dark' }) => {
    const beaconName = beaconPin.beaconName;
    const { isOffline, isTelemetryStale, dotCenterColor, title, hasOfflineNote } = getBeaconVisualState(beaconPin, mapTheme);
    const markerRef = useRef<L.Marker>(null);

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

    // Small badge marking an offline beacon that has a note to view (click target)
    const noteBadgeSizePx = Math.max(10, Math.round(beaconDotSizePx * 0.55));
    const noteBadge = hasOfflineNote
        ? `<div class=\"beacon-note-badge\" style=\"
            position:absolute;
            top:${beaconDotOffsetPx - noteBadgeSizePx * 0.3}px;
            left:${beaconDotOffsetPx + beaconDotSizePx - noteBadgeSizePx * 0.7}px;
            width:${noteBadgeSizePx}px;
            height:${noteBadgeSizePx}px;
            border-radius:50%;
            background:#f5a623;
            color:#1a1a2e;
            font-size:${Math.max(8, Math.round(noteBadgeSizePx * 0.7))}px;
            font-weight:700;
            font-family: Georgia, 'Times New Roman', serif;
            display:flex;
            align-items:center;
            justify-content:center;
            z-index:3;
            pointer-events:auto;
            cursor:pointer;
            box-shadow:0 0 0 1px rgba(0,0,0,0.4);
            line-height:1;
        \">i</div>`
        : '';

    // Memoized so Leaflet doesn't tear down and recreate the marker's DOM element on every
    // unrelated re-render (e.g. SignalR ticks for other beacons) — a fresh element would
    // silently detach the click listener bound to the (now stale) previous element below.
    const markerIcon = useMemo(() => L.divIcon({
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
                ${noteBadge}
                ${pingDiv}
            </div>
        `,
        iconSize: [outlineSize, outlineSize],
        iconAnchor: [outlineSize / 2, outlineSize / 2],
        popupAnchor: [0, -(beaconDotSizePx / 2 + 4)],
    }), [
        outlineSize,
        beaconDotSizePx,
        beaconDotOffsetPx,
        dotCenterColor,
        isTelemetryStale,
        beaconName,
        title,
        dottedOutline,
        telemetryStaleRing,
        noteBadge,
        pingDiv,
    ]);

    // Inject the same dark-mode popup CSS used by TelemetryMarker/PassengerTelemetryMarker so
    // the offline-note popup matches the rest of the map's Leaflet popups (idempotent: guarded
    // by element id, so it's a no-op if another marker already injected it).
    useEffect(() => {
        const styleId = 'leaflet-popup-darkmode-style';
        if (!document.getElementById(styleId)) {
            const style = document.createElement('style');
            style.id = styleId;
            style.innerHTML = `
                body[data-theme='dark'] .leaflet-popup-content-wrapper,
                .dark .leaflet-popup-content-wrapper {
                    background: #181a1b !important;
                    color: #f3f3f3 !important;
                    border: 1px solid #333 !important;
                    box-shadow: 0 0 8px rgba(0, 123, 255, 0.6);
                }
                body[data-theme='dark'] .leaflet-popup-content,
                .dark .leaflet-popup-content {
                    color: #f3f3f3 !important;
                }
                body[data-theme='dark'] .leaflet-popup-tip,
                .dark .leaflet-popup-tip {
                    background: #181a1b !important;
                    border: 1px solid #333 !important;
                    box-shadow: 0 0 8px rgba(0, 123, 255, 0.6);
                }
            `;
            document.head.appendChild(style);
        }
    }, []);

    // Bind a native Leaflet popup showing the offline note, toggled on click — matches the
    // popup pattern used for Amtrak/telemetry markers elsewhere on the map. Re-bound whenever
    // markerIcon changes since react-leaflet swaps the marker's underlying DOM element (via
    // Leaflet's setIcon) any time the icon reference changes.
    useEffect(() => {
        const marker = markerRef.current;
        if (!marker) return;

        if (!hasOfflineNote || !beaconPin.offlineNote) {
            marker.unbindPopup();
            return;
        }

        const popupContent = `
            <div>
                <strong>${escapeHtml(beaconName)}</strong><br/>
                <span style="color:#f5a623;font-weight:600;">Offline</span><br/>
                ${escapeHtml(beaconPin.offlineNote)}
            </div>
        `;
        marker.bindPopup(popupContent);

        let popupOpen = false;
        const handleClick = () => {
            if (popupOpen) {
                marker.closePopup();
            } else {
                marker.openPopup();
            }
            popupOpen = !popupOpen;
        };
        marker.on('click', handleClick);

        return () => {
            marker.off('click', handleClick);
            marker.unbindPopup();
        };
    }, [markerIcon, hasOfflineNote, beaconPin.offlineNote, beaconName]);

    return (
        <Marker
            ref={markerRef}
            key={`beacon-${beaconPin.beaconID ?? idx}-${beaconPin.latitude}-${beaconPin.longitude}`}
            position={[beaconPin.latitude, beaconPin.longitude]}
            pane="beaconPane"
            icon={markerIcon}
        />
    );
};

export default BeaconMarker;
