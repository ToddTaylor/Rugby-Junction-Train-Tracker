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

function TelemetryMarker({ pin }: { pin: MapPin }) {
    const markerRef = useRef<L.Marker>(null);
    const [opacity, setOpacity] = useState(() => getPinOpacity(pin.createdAt));

    useEffect(() => {
        // Update opacity every 30 seconds
        const interval = setInterval(() => {
            setOpacity(getPinOpacity(pin.createdAt));
        }, 30000);

        // Set initial opacity
        setOpacity(getPinOpacity(pin.createdAt));

        return () => clearInterval(interval);
    }, [pin.createdAt]);

    useEffect(() => {
        const marker = markerRef.current;
        if (!marker) return;

        const popupContent = `
      <strong>Train ID:</strong> ${pin.addressID}<br/>
      <strong>Direction:</strong> ${pin.direction || 'Unknown'}<br/>
      <strong>Source:</strong> ${pin.source}<br/>
      <strong>Moving:</strong> ${pin.moving === true ? "Yes" : pin.moving === false ? "No" : "Unknown"}<br/>
      <strong>Timestamp:</strong> ${format(parseISO(pin.createdAt), 'h:mm aa')}
    `;

        marker.bindPopup(popupContent);

        marker.on('mouseover', function () {
            marker.openPopup();
        });

        marker.on('mouseout', function () {
            marker.closePopup();
        });

        // Set marker opacity on load and when pin changes
        marker.setOpacity(opacity);

        return () => {
            marker.off('mouseover');
            marker.off('mouseout');
        };
    }, [pin, opacity]);

    const createCustomIcon = (direction?: string, moving?: Boolean) =>
        L.divIcon({
            html: ReactDOMServer.renderToString(
                <div style={{ opacity }}>
                    <ArrowMapPin direction={direction as any} moving={moving as any} />
                </div>
            ),
            className: '',
            iconSize: [20, 20],
            iconAnchor: [10, 0],
        });

    return (
        <Marker
            ref={markerRef}
            position={[pin.latitude, pin.longitude]}
            icon={createCustomIcon(pin.direction, pin.moving)}
            opacity={opacity}
        />
    );
}

export default TelemetryMarker;
