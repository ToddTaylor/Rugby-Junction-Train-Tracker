import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { useEffect, useRef, useState } from 'react';
import ArrowMapPin from './ArrowMapPin';
import ReactDOMServer from 'react-dom/server';
import { MapPin } from '../types/types';
import { parseISO } from 'date-fns/parseISO';
import { format } from 'date-fns';

function getPinBrightness(lastUpate: string, source?: string): number {
    const now = new Date();
    const created = parseISO(lastUpate);
    let minutes = (now.getTime() - created.getTime()) / 60000;

    // If source is "EOT", halve the thresholds
    const isEOT = source === "EOT";
    const t2 = isEOT ? 1 : 2;
    const t4 = isEOT ? 2 : 4;
    const t6 = isEOT ? 3 : 6;

    if (minutes < t2) return 1.0;
    if (minutes < t4) return 0.7;
    if (minutes < t6) return 0.4;
    return 0.4;
}

interface TelemetryMarkerProps {
    pin: MapPin;
    size: number;
}

const TelemetryMarker: React.FC<TelemetryMarkerProps> = ({ pin, size }) => {
    const markerRef = useRef<L.Marker>(null);
    const [brightness, setBrightness] = useState(() => getPinBrightness(pin.lastUpdate, pin.source));

    useEffect(() => {
        const interval = setInterval(() => {
            setBrightness(getPinBrightness(pin.lastUpdate, pin.source));
        }, 30000);
        setBrightness(getPinBrightness(pin.lastUpdate, pin.source));
        return () => clearInterval(interval);
    }, [pin.lastUpdate, pin.source]);

    useEffect(() => {
        const marker = markerRef.current;
        if (!marker) return;

        const popupContent = `
            <strong>Train ID:</strong> ${pin.addressID}<br/>
            <strong>Milepost:</strong> ${pin.milepost}<br/>
            <strong>Direction:</strong> ${pin.direction || 'Unknown'}<br/>
            <strong>Railroad:</strong> ${pin.railroad || 'Unknown'}<br/>
            <strong>Subdivision:</strong> ${pin.subdivision || 'Unknown'}<br/>
            <strong>Source:</strong> ${pin.source}<br/>
            <strong>Moving:</strong> ${pin.moving === true ? "Yes" : pin.moving === false ? "No" : "Unknown"}<br/>
            <strong>Timestamp:</strong> ${format(parseISO(pin.lastUpdate), 'h:mm aa')}
        `;

        marker.bindPopup(popupContent);

        marker.on('mouseover', function () {
            marker.openPopup();
        });

        marker.on('mouseout', function () {
            marker.closePopup();
        });

        return () => {
            marker.off('mouseover');
            marker.off('mouseout');
        };
    }, [pin, brightness]);

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
            className: 'telemetry-marker-z',
        });

    return (
        <Marker
            key={pin.id}
            ref={markerRef}
            position={[pin.latitude, pin.longitude]}
            icon={createCustomIcon(pin.direction, pin.moving)}
        />
    );
};

export default TelemetryMarker;
