import { useEffect, useRef } from 'react';
import type { RefObject } from 'react';
import { LatLngTuple, Map as LeafletMap } from 'leaflet';
import L from 'leaflet';
import { DefectDetector } from '../hooks/useDefectDetectors';
import { getBeaconDotSizePx } from '../utils/markerSizing';

type DefectDetectorLayerProps = {
    mapRef: RefObject<LeafletMap | null>;
    detectors: DefectDetector[];
    mapZoom: number;
    mapCenter: LatLngTuple;
    mapTheme: 'dark' | 'light';
};

function ensurePopupDarkModeStyle() {
    const styleId = 'leaflet-popup-darkmode-style';
    if (document.getElementById(styleId)) {
        return;
    }

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

function escapeHtml(value: string) {
    return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function buildPopupContent(detector: DefectDetector) {
    const location = detector.shortName
        ? `${detector.location}, ${detector.shortName}`
        : detector.location;
    const railroad = escapeHtml(detector.railroad || 'Unknown');
    const division = escapeHtml(detector.division || 'Unknown');
    const subdivision = escapeHtml(detector.subdivision || 'Unknown');
    const locationText = escapeHtml(location || 'Unknown');
    const milepost = escapeHtml(detector.milepost || 'Unknown');
    const frequency = escapeHtml(detector.frequency || 'Unknown');

    return `
        <div>
            <strong>${locationText}</strong><br/>
            ${railroad}<br/>
            ${division} Division<br/>
            ${subdivision} Subdivision<br/>
            MP ${milepost}<br/>
            Frequency: ${frequency}
        </div>
    `;
}

const DETECTOR_ORANGE = '#f28c18';
const DETECTOR_STROKE_DARK = '#ffd8a8';
const DETECTOR_STROKE_LIGHT = '#7c3b00';

function buildDetectorIconHtml(width: number, height: number, fontSize: number, mapTheme: 'dark' | 'light') {
    const strokeColor = mapTheme === 'dark' ? DETECTOR_STROKE_DARK : DETECTOR_STROKE_LIGHT;
    const textFill = '#000000';
    const textStroke = mapTheme === 'dark' ? '#fff4e8' : '#fff4e8';
    const textY = Math.round(height * 0.74);

    return `
        <svg width="${width}" height="${height}" viewBox="0 0 ${width} ${height}" xmlns="http://www.w3.org/2000/svg" style="overflow:visible;filter:drop-shadow(0 1px 2px rgba(0,0,0,0.45));">
            <polygon points="${Math.round(width / 2)},2 2,${height - 2} ${width - 2},${height - 2}" fill="${DETECTOR_ORANGE}" stroke="${strokeColor}" stroke-width="1.6" stroke-linejoin="round" />
            <text x="50%" y="${textY}" text-anchor="middle" font-size="${fontSize}" font-weight="800" font-family="Segoe UI, Arial, sans-serif" fill="${textFill}" stroke="${textStroke}" stroke-width="0.45" paint-order="stroke">D</text>
        </svg>
    `;
}

const DefectDetectorLayer: React.FC<DefectDetectorLayerProps> = ({
    mapRef,
    detectors,
    mapZoom,
    mapCenter,
    mapTheme
}) => {
    const detectorLayerRef = useRef<L.LayerGroup | null>(null);

    useEffect(() => {
        ensurePopupDarkModeStyle();
    }, []);

    useEffect(() => {
        return () => {
            if (detectorLayerRef.current && mapRef.current) {
                mapRef.current.removeLayer(detectorLayerRef.current);
                detectorLayerRef.current = null;
            }
        };
    }, [mapRef]);

    useEffect(() => {
        if (!mapRef.current) return;

        const map = mapRef.current;
        const pane = map.getPane('defectDetectorPane');
        if (!pane) {
            map.createPane('defectDetectorPane');
            const createdPane = map.getPane('defectDetectorPane');
            if (createdPane) {
                createdPane.style.zIndex = '430';
            }
        }

        if (!detectorLayerRef.current) {
            detectorLayerRef.current = L.layerGroup().addTo(map);
        }

        const layer = detectorLayerRef.current;
        layer.clearLayers();

        if (detectors.length === 0) {
            return;
        }

        const bounds = map.getBounds().pad(0.2);
        const beaconSize = getBeaconDotSizePx(mapZoom);
        const iconWidth = beaconSize + 8;
        const iconHeight = beaconSize + 6;
        const fontSize = Math.max(11, Math.round(beaconSize * 0.62));

        detectors.forEach(detector => {
            if (!bounds.contains([detector.lat, detector.lng])) return;

            const icon = L.divIcon({
                className: 'defect-detector-marker-icon',
                html: buildDetectorIconHtml(iconWidth, iconHeight, fontSize, mapTheme),
                iconSize: [iconWidth, iconHeight],
                iconAnchor: [iconWidth / 2, iconHeight * 0.72],
                popupAnchor: [0, -Math.round(iconHeight * 0.75)]
            });

            L.marker([detector.lat, detector.lng], {
                icon,
                pane: 'defectDetectorPane'
            })
                .bindPopup(buildPopupContent(detector), {
                    maxWidth: 260,
                    autoPanPadding: [16, 16]
                })
                .addTo(layer);
        });
    }, [detectors, mapZoom, mapCenter, mapTheme, mapRef]);

    return null;
};

export default DefectDetectorLayer;