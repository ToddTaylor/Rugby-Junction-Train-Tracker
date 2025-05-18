import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { useEffect, useRef, useState } from 'react';
import ArrowMapPin from './ArrowMapPin';
import ReactDOMServer from 'react-dom/server';
import { MapPin } from '../types/types';
import { parseISO } from 'date-fns/parseISO';
import { format } from 'date-fns';

function getPinOpacity(createdAt: string): number {
    const now = new Date();
    const created = parseISO(createdAt);
    const minutes = (now.getTime() - created.getTime()) / 60000;
    if (minutes < 5) return 1.0;
    if (minutes < 10) return 0.75;
    if (minutes < 15) return 0.5;
    return 0.5;
}

interface TelemetryMarkersProps {
    pins: { [id: string]: MapPin };
}

function TelemetryMarkers({ pins: telemetryPins }: TelemetryMarkersProps) {
    // Render a Marker for each pin in the map
    return (
        <>
            {Object.values(telemetryPins).map((telemetryPin) => {
                const markerRef = useRef<L.Marker>(null);
                const [opacity, setOpacity] = useState(() => getPinOpacity(telemetryPin.createdAt));

                useEffect(() => {
                    const interval = setInterval(() => {
                        setOpacity(getPinOpacity(telemetryPin.createdAt));
                    }, 30000);
                    setOpacity(getPinOpacity(telemetryPin.createdAt));
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

                    marker.setOpacity(opacity);

                    return () => {
                        marker.off('mouseover');
                        marker.off('mouseout');
                    };
                }, [telemetryPin, opacity]);

                const size = 20; // Size of the marker in pixels
                const createCustomIcon = (direction?: string, moving?: Boolean) =>
                    L.divIcon({
                        html: ReactDOMServer.renderToString(
                            <div style={{ opacity }}>
                                <ArrowMapPin direction={direction as any} moving={moving as any} />
                            </div>
                        ),
                        className: '',
                        iconSize: [size, size],
                        iconAnchor: [size / 2, size / 2],
                    });

                return (
                    <Marker
                        key={telemetryPin.id}
                        ref={markerRef}
                        position={[telemetryPin.latitude, telemetryPin.longitude]}
                        icon={createCustomIcon(telemetryPin.direction, telemetryPin.moving)}
                        opacity={opacity}
                    />
                );
            })}
        </>
    );
}

export default TelemetryMarkers;
