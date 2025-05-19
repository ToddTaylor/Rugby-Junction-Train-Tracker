import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { useEffect, useRef, useState } from 'react';
import ArrowMapPin from './ArrowMapPin';
import ReactDOMServer from 'react-dom/server';
import { MapPin } from '../types/types';
import { parseISO } from 'date-fns/parseISO';
import { format } from 'date-fns';

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
            {Object.values(telemetryPins).map((telemetryPin) => {
                const markerRef = useRef<L.Marker>(null);
                const [brightness, setBrightness] = useState(() => getPinBrightness(telemetryPin.createdAt));

                useEffect(() => {
                    const interval = setInterval(() => {
                        setBrightness(getPinBrightness(telemetryPin.createdAt));
                    }, 30000);
                    setBrightness(getPinBrightness(telemetryPin.createdAt));
                    return () => clearInterval(interval);
                }, [telemetryPin.createdAt]);

                useEffect(() => {
                    const marker = markerRef.current;
                    if (!marker) return;

                    const popupContent = `
                        <strong>Train ID:</strong> ${telemetryPin.addressID}<br/>
                        <strong>Direction:</strong> ${telemetryPin.direction || 'Unknown'}<br/>
                        <strong>Source:</strong> ${telemetryPin.source}<br/>
                        <strong>Moving:</strong> ${telemetryPin.moving === true ? "Yes" : telemetryPin.moving === false ? "No" : "Unknown"}<br/>
                        <strong>Timestamp:</strong> ${format(parseISO(telemetryPin.createdAt), 'h:mm aa')}
                    `;

                    marker.bindPopup(popupContent);

                    marker.on('mouseover', function () {
                        marker.openPopup();
                    });

                    marker.on('mouseout', function () {
                        marker.closePopup();
                    });

                    // No marker.setOpacity here, handled by CSS filter

                    return () => {
                        marker.off('mouseover');
                        marker.off('mouseout');
                    };
                }, [telemetryPin, brightness]);

                const createCustomIcon = (direction?: string, moving?: Boolean) =>
                    L.divIcon({
                        html: ReactDOMServer.renderToString(
                            <div style={{
                                filter: `brightness(${brightness})`,
                                width: size,
                                height: size
                            }}>
                                <ArrowMapPin direction={direction as any} moving={moving as any} />
                            </div>
                        ),
                        iconSize: [size, size],
                        iconAnchor: [size / 2, size / 2],
                        // Ensure TelemetryMarkers have higher z-index than BeaconMarkers
                        className: 'telemetry-marker-z',
                    });

                return (
                    <Marker
                        key={telemetryPin.id}
                        ref={markerRef}
                        position={[telemetryPin.latitude, telemetryPin.longitude]}
                        icon={createCustomIcon(telemetryPin.direction, telemetryPin.moving)}
                    />
                );
            })}
        </>
    );
}

export default TelemetryMarkers;
