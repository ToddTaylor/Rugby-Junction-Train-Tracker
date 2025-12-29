import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { useEffect, useRef, useState } from 'react';
import { parseISO } from 'date-fns/parseISO';
import { format } from 'date-fns';
import { getTrackedMapPins, addTrackedMapPin, removeTrackedMapPin, getTrackedColor } from '../services/trackedPins';
import ReactDOMServer from 'react-dom/server';
import { ArrowIcon } from './ArrowIcon'; // adjust import as needed
import { UnknownIcon } from './UnknownIcon'; 
import { MapPin } from '../types/MapPin';

function getPinBrightness(lastUpdate: string, maxPinAgeMinutes?: number): number {
    const now = new Date();
    const created = parseISO(lastUpdate);
    maxPinAgeMinutes = maxPinAgeMinutes || Number(import.meta.env.VITE_MAX_PIN_AGE_MINUTES);
    const minutes = (now.getTime() - created.getTime()) / 60000;

    // Exponential decay from 1.0 to minBrightness over maxPinAgeMinutes
    // Pin will be removed from map when maxPinAgeMinutes is reached
    const minBrightness = 0.3;
    const normalizedTime = Math.min(minutes / maxPinAgeMinutes, 1.0);
    const brightness = minBrightness + (1.0 - minBrightness) * Math.pow(1 - normalizedTime, 4);
    
    return brightness;
}

interface TelemetryMarkerProps {
    pin: MapPin;
    size: number;
    maxPinAgeMinutes?: number;
}

const formatDirection = (dir?: string | null): string => {
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

const TelemetryMarker: React.FC<TelemetryMarkerProps & { mapTheme: 'dark' | 'light' }> = ({ pin, size, maxPinAgeMinutes, mapTheme }) => {
    const markerRef = useRef<L.Marker>(null);
    const shouldReopenPopupRef = useRef(false);
    const [brightness, setBrightness] = useState(() =>
        getPinBrightness(pin.lastUpdate, maxPinAgeMinutes)
    );
    const [isTracked, setIsTracked] = useState(() =>
        !!getTrackedMapPins().find(tp => String(tp.id) === String(pin.id))
    );
    const [trackColor, setTrackColor] = useState<string | undefined>(() =>
        getTrackedColor(String(pin.id))
    );

    useEffect(() => {
        const checkTracked = () => {
            const tracked = getTrackedMapPins().find(tp => String(tp.id) === String(pin.id));
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
            const newBrightness = getPinBrightness(pin.lastUpdate, maxPinAgeMinutes);
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

        // Add a unique id to the trackText span for event delegation
        const trackTextId = `track-text-${pin.id}`;
        const trackingIcon = isTracked
            ? `<img src='/icons/tracking-on.svg' alt='Tracking' style='height:16px;width:16px;vertical-align:middle;margin-right:6px;' />`
            : `<img src='/icons/tracking-off.svg' alt='Not Tracking' style='height:16px;width:16px;vertical-align:middle;margin-right:6px;' />`;
        const trackText = isTracked
            ? 'Tracking'
            : 'Not Tracking';

        const popupContent = `
            ${pin.beaconName ? `<strong>${pin.beaconName}</strong><br/>` : ''}
            ${pin.railroad?.trim() ? `${pin.railroad} ${pin.subdivision + ' Sub' || ''}<br/>` : ''}
            MP ${pin.milepost}<br/>
            ${formatDirection(pin.direction)}<br/>
            ${pin.moving === true ? "Moving<br/>" : pin.moving === false ? "Not Moving<br/>" : ''}
            ${format(parseISO(pin.lastUpdate), 'h:mm aa')}<br/>
            ${addressLines}
            <span id='${trackTextId}' style='cursor:pointer;text-decoration:underline;color:#007bff;display:inline-flex;align-items:center;'>${trackingIcon}${trackText}</span>
        `;

        marker.bindPopup(popupContent);

        let popupOpen = false;
        const handleMarkerClick = () => {
            if (popupOpen) {
                marker.closePopup();
                popupOpen = false;
            } else {
                marker.openPopup();
                popupOpen = true;
            }
        };
        marker.on('click', handleMarkerClick);

        // Attach click handler to trackText in popup DOM on popupopen, and re-attach if popup content changes
        let trackTextEl: HTMLElement | null = null;
        let observer: MutationObserver | null = null;
        const handleTrackTextClick = (e: any) => {
            e.preventDefault();
            e.stopPropagation();
            shouldReopenPopupRef.current = true;
            if (isTracked) {
                removeTrackedMapPin(String(pin.id));
                setIsTracked(false);
                setTrackColor(undefined);
            } else {
                addTrackedMapPin(String(pin.id), pin.beaconID, pin.beaconName);
                setIsTracked(true);
                setTrackColor(getTrackedColor(String(pin.id)));
            }
        };
        const attachHandler = () => {
            trackTextEl = document.getElementById(trackTextId);
            if (trackTextEl) {
                trackTextEl.addEventListener('click', handleTrackTextClick);
            }
        };
        const detachHandler = () => {
            if (trackTextEl) {
                trackTextEl.removeEventListener('click', handleTrackTextClick);
                trackTextEl = null;
            }
        };
        const onPopupOpen = () => {
            attachHandler();
            // Watch for popup content changes and re-attach handler
            const popupNode = document.querySelector('.leaflet-popup-content');
            if (popupNode) {
                observer = new MutationObserver(() => {
                    detachHandler();
                    attachHandler();
                });
                observer.observe(popupNode, { childList: true, subtree: true });
            }
        };
        const onPopupClose = () => {
            detachHandler();
            if (observer) {
                observer.disconnect();
                observer = null;
            }
        };
        marker.on('popupopen', onPopupOpen);
        marker.on('popupclose', onPopupClose);

        // Reopen popup after tracking state changes to show updated content
        if (shouldReopenPopupRef.current) {
            shouldReopenPopupRef.current = false;
            const isPopupOpen = marker.isPopupOpen();
            if (isPopupOpen) {
                setTimeout(() => {
                    marker.closePopup();
                    setTimeout(() => {
                        marker.openPopup();
                    }, 100);
                }, 50);
            }
        }

        return () => {
            marker.off('click', handleMarkerClick);
            marker.off('popupopen', onPopupOpen);
            marker.off('popupclose', onPopupClose);
            detachHandler();
            if (observer) {
                observer.disconnect();
            }
        };
    }, [pin, brightness, isTracked]);

    // Use ArrowMapPin as the icon, with rotation and border color
    const createCustomIcon = () => {
        const iconSrc = getArrowIconSrc(pin.direction, pin.moving != null ? !!pin.moving : undefined, mapTheme);

        if (!pin.direction) {
            return L.divIcon({
                html: ReactDOMServer.renderToString(
                    <UnknownIcon
                        iconSrc={iconSrc}
                        brightness={brightness}
                        trackColor={trackColor}
                        size={size}
                        isLocal={pin.isLocal}
                    />
                ),
                iconSize: [size, size],
                iconAnchor: [size / 2, size / 2], // <-- absolute center of marker
                popupAnchor: [0, -size / 2],      // popup appears above the marker, scales with size
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
                    isLocal={pin.isLocal}
                />
            ),
            iconSize: [size, size],
            iconAnchor: [size / 2, size / 2], // <-- absolute center of marker
            popupAnchor: [0, -size / 2],      // popup appears above the marker, scales with size
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

function getArrowIconSrc(direction?: string | null, moving?: boolean, mapTheme: 'dark' | 'light' = 'dark'): string {
    const suffix = mapTheme === 'dark' ? '-dark' : '-light';
    const cacheBuster = import.meta.env.VITE_APP_VERSION
        ? `?v=${import.meta.env.VITE_APP_VERSION}`
        : `?t=${Date.now()}`; // fallback to timestamp if version not set

    if (!direction) {
        if (moving === true) return `/icons/unknown-green${suffix}.svg${cacheBuster}`;
        if (moving === false) return `/icons/unknown-red${suffix}.svg${cacheBuster}`;
        return `/icons/unknown${suffix}.svg${cacheBuster}`;
    }
    if (moving === true) return `/icons/arrow-green${suffix}.svg${cacheBuster}`;
    if (moving === false) return `/icons/arrow-red${suffix}.svg${cacheBuster}`;
    return `/icons/arrow${suffix}.svg${cacheBuster}`;
}

function getRotation(direction?: string | null): number {
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
