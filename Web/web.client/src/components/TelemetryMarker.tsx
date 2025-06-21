import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { useEffect, useRef, useState } from 'react';
import { MapPin } from '../types/types';
import { parseISO } from 'date-fns/parseISO';
import { format } from 'date-fns';
import { getTrackedMapPins, addTrackedMapPin, removeTrackedMapPin, getTrackedColor } from './trackUtils';
import ReactDOMServer from 'react-dom/server';
import { ArrowIcon } from './ArrowIcon'; // adjust import as needed
import { UnknownIcon } from './UnknownIcon'; 

function getPinBrightness(lastUpdate: string, addresses?: { source: string }[], maxPinAgeMinutes?: number): number {
    const now = new Date();
    const created = parseISO(lastUpdate);
    maxPinAgeMinutes = maxPinAgeMinutes || Number(import.meta.env.VITE_MAX_PIN_AGE_MINUTES);
    let minutes = (now.getTime() - created.getTime()) / 60000;

    // Check if any address has source "EOT"
    const isEOT = Array.isArray(addresses) && addresses.some(a => a.source === "EOT");
    const t2 = isEOT ? maxPinAgeMinutes / 6 : maxPinAgeMinutes * 1/3;
    const t4 = isEOT ? maxPinAgeMinutes / 3 : maxPinAgeMinutes * 2/3;
    const t6 = isEOT ? maxPinAgeMinutes / 2 : maxPinAgeMinutes;

    if (minutes < t2) return 1.0;
    if (minutes < t4) return 0.7;
    if (minutes < t6) return 0.4;
    return 0.4;
}

interface TelemetryMarkerProps {
    pin: MapPin;
    size: number;
    maxPinAgeMinutes?: number;
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

const TelemetryMarker: React.FC<TelemetryMarkerProps> = ({ pin, size, maxPinAgeMinutes }) => {
    const markerRef = useRef<L.Marker>(null);
    const [brightness, setBrightness] = useState(() =>
        getPinBrightness(pin.lastUpdate, pin.addresses, maxPinAgeMinutes)
    );
    const [isTracked, setIsTracked] = useState(() =>
        !!getTrackedMapPins().find(tp => tp.id === String(pin.id))
    );
    const [trackColor, setTrackColor] = useState<string | undefined>(() =>
        getTrackedColor(String(pin.id))
    );

    useEffect(() => {
        const checkTracked = () => {
            const tracked = getTrackedMapPins().find(tp => tp.id === String(pin.id));
            setIsTracked(!!tracked);
            setTrackColor(tracked?.color);
        };
        window.addEventListener('storage', checkTracked);
        const interval = setInterval(checkTracked, 60 * 1000);
        checkTracked();
        return () => {
            window.removeEventListener('storage', checkTracked);
            clearInterval(interval);
        };
    }, [pin.id]);

    useEffect(() => {
        const update = () => {
            const newBrightness = getPinBrightness(pin.lastUpdate, pin.addresses, maxPinAgeMinutes);
            setBrightness(newBrightness);
        };
        update();
        const interval = setInterval(update, 30000);
        return () => clearInterval(interval);
    }, [pin.lastUpdate, pin.addresses, maxPinAgeMinutes]);

    useEffect(() => {
        const marker = markerRef.current;
        if (!marker) return;

        let addressLines = '';
        if (Array.isArray(pin.addresses)) {
            addressLines = pin.addresses
                .map((a: { source: string; addressID: number }) => `${a.addressID} ${a.source}<br/>`)
                .join('');
        }

        const trackText = isTracked ? "Click to Untrack Train" : "Click to Track Train";

        const popupContent = `
            ${pin.railroad?.trim() ? `<strong>${pin.railroad} ${pin.subdivision + ' Sub' || ''}</strong><br/>` : ''}
            MP ${pin.milepost}<br/>
            ${formatDirection(pin.direction)}<br/>
            ${pin.moving === true ? "Moving<br/>" : pin.moving === false ? "Not Moving<br/>" : ''}
            ${format(parseISO(pin.lastUpdate), 'h:mm aa')}<br/>
            ${addressLines}
            <span style="color:#1976d2;font-weight:bold;">${trackText}</span>
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
    }, [pin, brightness, isTracked]);

    // Handle click on the marker to track/untrack
    useEffect(() => {
        const marker = markerRef.current;
        if (!marker) return;

        const handleClick = () => {
            if (isTracked) {
                removeTrackedMapPin(String(pin.id));
                setIsTracked(false);
                setTrackColor(undefined);
            } else {
                addTrackedMapPin(String(pin.id));
                setIsTracked(true);
                setTrackColor(getTrackedColor(String(pin.id)));
            }
        };

        marker.on('click', handleClick);

        return () => {
            marker.off('click', handleClick);
        };
    }, [isTracked, pin.id]);

    // Use ArrowMapPin as the icon, with rotation and border color
    const createCustomIcon = () => {
        const iconSrc = getArrowIconSrc(pin.direction, pin.moving != null ? !!pin.moving : undefined);

        if (!pin.direction) {
            return L.divIcon({
                html: ReactDOMServer.renderToString(
                    <UnknownIcon
                        iconSrc={iconSrc}
                        brightness={brightness}
                        trackColor={trackColor}
                        size={size}
                    />
                ),
                iconSize: [size, size],
                iconAnchor: [size / 2, size / 2],
                className: 'telemetry-marker-z',
            });
        }

        return L.divIcon({
            html: ReactDOMServer.renderToString(
                <ArrowIcon
                    iconSrc={iconSrc}
                    brightness={brightness}
                    trackColor={trackColor}
                    size={size}
                    rotation={getRotation(pin.direction)}
                />
            ),
            iconSize: [size, size],
            iconAnchor: [size / 2, size / 2],
            className: 'telemetry-marker-z',
        });
    };

    return (
        <Marker
            key={pin.id}
            ref={markerRef}
            position={[pin.latitude, pin.longitude]}
            icon={createCustomIcon()}
            pane="telemetryPane"
        />
    );
};

function getArrowIconSrc(direction?: string, moving?: boolean): string {
    if (!direction) {
        if (moving === true) return '/icons/unknown-green.svg';
        if (moving === false) return '/icons/unknown-red.svg';
        return '/icons/unknown.svg';
    }
    if (moving === true) return '/icons/arrow-green.svg';
    if (moving === false) return '/icons/arrow-red.svg';
    return '/icons/arrow.svg';
}

function getRotation(direction?: string): number {
    switch ((direction || '').toUpperCase()) {
        case 'N': return 0;
        case 'NE': return 45;
        case 'E': return 90;
        case 'SE': return 135;
        case 'S': return 180;
        case 'SW': return 225;
        case 'W': return 270;
        case 'NW': return 315;
        default: return 0;
    }
}

export default TelemetryMarker;
