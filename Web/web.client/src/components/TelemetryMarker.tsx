import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { useEffect, useRef, useState } from 'react';
import ArrowMapPin from './ArrowMapPin';
import ReactDOMServer from 'react-dom/server';
import { MapPin } from '../types/types';
import { parseISO } from 'date-fns/parseISO';
import { format } from 'date-fns';

// Replace all uses of pin.source with a check for any EOT in pin.addresses

function getPinBrightness(lastUpdate: string, addresses?: { source: string }[]): number {
    const now = new Date();
    const created = parseISO(lastUpdate);
    let minutes = (now.getTime() - created.getTime()) / 60000;

    // Check if any address has source "EOT"
    const isEOT = Array.isArray(addresses) && addresses.some(a => a.source === "EOT");
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

const formatDirection = (dir?: string): string => {
    if (!dir) return 'Unknown Direction';

    const map: Record<string, string> = {
        N: 'Northbound',
        S: 'Southbound',
        E: 'Eastbound',
        W: 'Westbound',
        NE: 'Northeastbound',
        NW: 'Northwestbound',
        SE: 'Southeastbound',
        SW: 'Southwestbound',
        EN: 'Northeastbound',
        ES: 'Southeastbound',
        WN: 'Northwestbound',
        WS: 'Southwestbound',
    };

    return map[dir.toUpperCase()] || 'Unknown Direction';
};

const TelemetryMarker: React.FC<TelemetryMarkerProps> = ({ pin, size }) => {
    const markerRef = useRef<L.Marker>(null);
    const [brightness, setBrightness] = useState(() => getPinBrightness(pin.lastUpdate, pin.addresses));

    useEffect(() => {
        const interval = setInterval(() => {
            setBrightness(getPinBrightness(pin.lastUpdate, pin.addresses));
        }, 30000);
        setBrightness(getPinBrightness(pin.lastUpdate, pin.addresses));
        return () => clearInterval(interval);
    }, [pin.lastUpdate, pin.addresses]);

    useEffect(() => {
        const marker = markerRef.current;
        if (!marker) return;

        // Build address lines
        let addressLines = '';
        if (Array.isArray(pin.addresses)) {
            addressLines = pin.addresses
                .map((a: { source: string; addressID: number }) => `${a.addressID} ${a.source}<br/>`)
                .join('');
        }

        const popupContent = `
            ${pin.railroad?.trim() ? `<strong>${pin.railroad} ${pin.subdivision + ' Sub' || ''}</strong><br/>` : ''}
            MP ${pin.milepost}<br/>
            ${formatDirection(pin.direction)}<br/>
            ${pin.moving === true ? "Moving<br/>" : pin.moving === false ? "Not Moving<br/>" : ''}
            ${format(parseISO(pin.lastUpdate), 'h:mm aa')}<br/>
            ${addressLines}
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
            pane="telemetryPane"
        />
    );
};

export default TelemetryMarker;
