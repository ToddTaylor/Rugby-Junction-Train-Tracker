import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { useEffect, useRef, useState } from 'react';
import ArrowMapPin from './ArrowMapPin';
import ReactDOMServer from 'react-dom/server';
import { MapPin } from '../types/types';
import { parseISO } from 'date-fns/parseISO';
import { format } from 'date-fns';
import TelemetryMarker from './TelemetryMarker';

// Replace getPinOpacity with getPinBrightness
function getPinBrightness(createdAt: string): number {
    const now = new Date();
    const created = parseISO(createdAt);
    const minutes = (now.getTime() - created.getTime()) / 60000;
    if (minutes < 5) return 1.0;
    if (minutes < 10) return 0.7;
    if (minutes < 15) return 0.4;
    return 0.4;
}

// Add dynamic marker sizing based on zoom
function getMarkerSize(zoom: number): number {
    // Base size at zoom 11 is 20px, scale up/down with zoom
    // Clamp between 10px and 40px for usability
    return Math.max(10, Math.min(40, 20 + (zoom - 11) * 2));
}

interface TelemetryMarkersProps {
    pins: { [id: string]: MapPin };
    zoom: number;
}

function TelemetryMarkers({ pins: telemetryPins, zoom }: TelemetryMarkersProps) {
    const size = getMarkerSize(zoom);

    return (
        <>
            {Object.values(telemetryPins).map((telemetryPin) => (
                <TelemetryMarker key={telemetryPin.id} pin={telemetryPin} size={size} />
            ))}
        </>
    );
}

export default TelemetryMarkers;
