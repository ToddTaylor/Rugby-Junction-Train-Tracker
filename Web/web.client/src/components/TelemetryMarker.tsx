import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { useEffect, useRef, useState } from 'react';
import { parseISO } from 'date-fns/parseISO';
import { format } from 'date-fns';
import { addTrackedMapPin, removeTrackedMapPin, getTrackedPinSymbol, updateTrackedPinSymbol, refreshTrackedPinsFromApi, copyTrackedPinShareUrl } from '../services/trackedPins';
// Extend window type for setTrackedPinsStateFromApi
declare global {
    interface Window {
        setTrackedPinsStateFromApi?: (pins: import('../services/trackedPins').TrackedPin[]) => void;
    }
}
import type { TrackedPin } from '../services/trackedPins';
import ReactDOMServer from 'react-dom/server';
import { ArrowIcon } from './ArrowIcon'; // adjust import as needed
import { UnknownIcon } from './UnknownIcon';
import TrackSymbolModal from './TrackSymbolModal';
import { MapPin } from '../types/MapPin';
import { CopyIcon } from './CopyIcon';

const ICON_CACHE_BUSTER = import.meta.env.VITE_APP_VERSION
    ? `?v=${import.meta.env.VITE_APP_VERSION}`
    : '';

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
    trackedPins: TrackedPin[];
    longitudeOffset?: number;
    hourFormat?: string;
    canViewSupportAddresses?: boolean;
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

const TelemetryMarker: React.FC<TelemetryMarkerProps & { mapTheme: 'dark' | 'light' }> = ({ pin, size, maxPinAgeMinutes, mapTheme, trackedPins, longitudeOffset = 0, hourFormat = '24', canViewSupportAddresses = false }) => {
    // hourFormat is destructured from props with default '24'
    // Inject dark mode popup CSS override once per page load
    useEffect(() => {
        const styleId = 'leaflet-popup-darkmode-style';
        if (!document.getElementById(styleId)) {
            const style = document.createElement('style');
            style.id = styleId;
            style.innerHTML = `
                /* Dark mode override: only background and text color, do not touch padding or box model */
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
    const markerRef = useRef<L.Marker>(null);
    const shouldReopenPopupRef = useRef(false);
    const [brightness, setBrightness] = useState(() =>
        getPinBrightness(pin.lastUpdate, maxPinAgeMinutes)
    );
    const [isTracked, setIsTracked] = useState(() =>
        !!trackedPins.find(tp => String(tp.id) === String(pin.id))
    );
    const [trackColor, setTrackColor] = useState<string | undefined>(() =>
        trackedPins.find(tp => String(tp.id) === String(pin.id))?.color
    );
    const [modalOpen, setModalOpen] = useState(false);
    const [modalSymbol, setModalSymbol] = useState('');
    const [isEditingTrack, setIsEditingTrack] = useState(false);

    const addressesForModal = Array.isArray(pin.addresses)
        ? pin.addresses.map(addr => ({ id: String(addr.addressID), source: addr.source }))
        : [];

    // Accept a callback prop to update trackedPins in parent (RailMap)
    // If not provided, fallback to no-op
    const updateTrackedPinsFromApi = async () => {
        if (typeof window.setTrackedPinsStateFromApi === 'function') {
            const pins = await refreshTrackedPinsFromApi();
            window.setTrackedPinsStateFromApi(pins);
        }
    };

    const handleModalSave = async (symbol: string) => {
        try {
            if (isEditingTrack) {
                await updateTrackedPinSymbol(String(pin.id), symbol);
            } else {
                await addTrackedMapPin(String(pin.id), pin.beaconID, pin.subdivisionID, pin.beaconName, symbol || undefined);
            }
            await updateTrackedPinsFromApi();
            setModalOpen(false);
        } catch (error) {
            console.error('Failed to save tracked pin:', error);
        }
    };

    const handleModalUntrack = async () => {
        try {
            await removeTrackedMapPin(String(pin.id));
            await updateTrackedPinsFromApi();
            setModalOpen(false);
        } catch (error) {
            console.error('Failed to untrack pin:', error);
        }
    };

    useEffect(() => {
        const tracked = trackedPins.find(tp => String(tp.id) === String(pin.id));
        setIsTracked(!!tracked);
        setTrackColor(tracked?.color);
    }, [trackedPins, pin.id]);

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
        if (canViewSupportAddresses && Array.isArray(pin.addresses) && pin.addresses.length > 0) {
            const activeAddressColor = mapTheme === 'dark' ? '#ffffff' : '#000000';
            const inactiveAddressColor = '#9ca3af';
            addressLines = pin.addresses
                .map((a: { source: string; addressID: number; isActive?: boolean }) => {
                    const color = a.isActive ? activeAddressColor : inactiveAddressColor;
                    return `<span style='color:${color};'>${a.addressID} ${a.source}</span><br/>`;
                })
                .join('');
        } else if (!canViewSupportAddresses) {
            // For Viewer role: show available source types (for example HOT/EOT/DPU).
            const sourceTypesLabel = Array.isArray(pin.addressSourceTypes) && pin.addressSourceTypes.length > 0
                ? pin.addressSourceTypes.join(' / ')
                : '';
            const dpuColor = mapTheme === 'dark' ? '#d5d9df' : '#4b5563';
            addressLines = sourceTypesLabel
                ? `<span style='color:${dpuColor};font-size:0.9em;'>${sourceTypesLabel}</span><br/>`
                : '';
        }

        // Add a unique id to the trackText span for event delegation
        const trackTextId = `track-text-${pin.id}`;
        const shareTextId = `share-text-${pin.id}`;
        const shareStatusId = `share-status-${pin.id}`;
        const trackedPinForLabel =
            trackedPins.find(tp => String(tp.id) === String(pin.id)) ||
            (pin.shareCode ? trackedPins.find(tp => tp.shareCode === pin.shareCode) : undefined);
        const trackedSymbolLabel = trackedPinForLabel?.symbol?.trim();
        const displayTrainLabel = trackedSymbolLabel || (pin.shareCode ? `Train ${pin.shareCode}` : '');
        const isCoarsePointer = typeof window !== 'undefined' && window.matchMedia('(pointer: coarse)').matches;
        const actionRowPadding = isCoarsePointer ? '4px 0' : '1px 0';
        const actionRowGap = isCoarsePointer ? '10px' : '6px';
        const trackingBadgeColor = isTracked ? (trackColor || '#FFD700') : '#cccccc';        const trackingBadgeBorder = isTracked ? '1px solid rgba(0, 0, 0, 0.5)' : '1px solid rgba(0, 0, 0, 0.3)';
        const trackingBadgeTextColor = isTracked ? '#000' : '#666';
        const trackingBadge = `<span style='width:14px;height:14px;background-color:${trackingBadgeColor};border-radius:50%;border:${trackingBadgeBorder};display:inline-flex;align-items:center;justify-content:center;font-size:9px;font-weight:900;color:${trackingBadgeTextColor};line-height:14px;flex-shrink:0;'>T</span>`;
        const shareCodeLine = pin.shareCode
            ? `<span id='${trackTextId}' style='cursor:pointer;display:inline-flex;align-items:center;gap:6px;padding:${actionRowPadding};margin-bottom:${actionRowGap};'><span style='display:inline-flex;align-items:center;'>${trackingBadge}</span><span style='text-decoration:${isTracked ? 'underline' : 'none'};color:${isTracked ? (trackColor || '#FFD700') : 'inherit'};font-weight:${isTracked ? 700 : 400};'>${displayTrainLabel}</span></span><br/>`
            : '';
        const copyIcon = ReactDOMServer.renderToStaticMarkup(
            <CopyIcon size={14} color={mapTheme === 'dark' ? '#d5d9df' : '#4b5563'} />
        );
        const trackText = isTracked
            ? 'Tracking'
            : 'Not Tracking';

        // Use only inline color for the track link, not for the popup background
        const popupContent = `
            <div>
                ${pin.beaconName ? `<strong>${pin.beaconName}</strong><br/>` : ''}
                ${pin.railroad?.trim() ? `${pin.railroad} ${pin.subdivision + ' Sub' || ''}<br/>` : ''}
                MP ${pin.milepost}<br/>
                ${formatDirection(pin.direction)}<br/>
                ${pin.moving === true ? "Moving<br/>" : pin.moving === false ? "Not Moving<br/>" : ''}
                ${(() => {
                    try {
                        if (hourFormat === '12') {
                            return format(parseISO(pin.lastUpdate), 'h:mm aa');
                        } else {
                            return format(parseISO(pin.lastUpdate), 'HH:mm');
                        }
                    } catch {
                        return pin.lastUpdate;
                    }
                })()}<br/>
                ${shareCodeLine}
                ${addressLines}
                ${pin.shareCode ? `<span id='${shareTextId}' title='Copy share link' style='cursor:pointer;display:inline-flex;align-items:center;gap:6px;padding:${actionRowPadding};margin-top:${isCoarsePointer ? '2px' : '0'};margin-bottom:6px;color:${mapTheme === 'dark' ? '#d5d9df' : '#4b5563'};'>${copyIcon}<span style='text-decoration:underline;'>Share</span><span id='${shareStatusId}' style='font-size:12px;opacity:0;transition:opacity 0.2s ease;'></span></span>` : ''}
                ${pin.shareCode ? '' : `<span id='${trackTextId}' style='cursor:pointer;text-decoration:underline;display:inline-flex;align-items:center;gap:6px;padding:${actionRowPadding};'><span style='display:inline-flex;align-items:center;'>${trackingBadge}</span>${trackText}</span>`}
            </div>
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
        let shareTextEl: HTMLElement | null = null;
        let observer: MutationObserver | null = null;
        let shareFeedbackTimeout: ReturnType<typeof setTimeout> | null = null;
        const updateShareStatus = (isSuccess: boolean) => {
            const shareStatusEl = document.getElementById(shareStatusId);
            if (!shareStatusEl) {
                return;
            }

            const badgePadding = isCoarsePointer ? '4px 8px' : '3px 6px';
            if (isSuccess) {
                const iconSize = isCoarsePointer ? '14px' : '12px';
                const iconFontSize = isCoarsePointer ? '10px' : '9px';
                shareStatusEl.innerHTML = `<span style="display:inline-flex;align-items:center;gap:4px;color:#14532d;background:rgba(220,252,231,0.95);border:1px solid rgba(34,197,94,0.35);border-radius:9999px;padding:${badgePadding};line-height:1;white-space:nowrap;font-size:12px;pointer-events:none;"><span style="width:${iconSize};height:${iconSize};border-radius:9999px;background:#16a34a;color:#fff;display:inline-flex;align-items:center;justify-content:center;font-size:${iconFontSize};font-weight:700;">✓</span>Copied!</span>`;
            } else {
                shareStatusEl.innerHTML = `<span style="display:inline-flex;align-items:center;gap:4px;color:#7f1d1d;background:rgba(254,226,226,0.95);border:1px solid rgba(248,113,113,0.45);border-radius:9999px;padding:${badgePadding};line-height:1;white-space:nowrap;font-size:12px;pointer-events:none;">Failed</span>`;
            }
            shareStatusEl.style.opacity = '1';

            if (shareFeedbackTimeout) {
                clearTimeout(shareFeedbackTimeout);
            }

            shareFeedbackTimeout = setTimeout(() => {
                const currentShareStatusEl = document.getElementById(shareStatusId);
                if (!currentShareStatusEl) {
                    return;
                }

                currentShareStatusEl.style.opacity = '0';
                currentShareStatusEl.textContent = '';
            }, 3000);
        };
        const handleTrackTextClick = (e: any) => {
            e.preventDefault();
            e.stopPropagation();
            shouldReopenPopupRef.current = true;
            if (isTracked) {
                // Open modal to edit or untrack existing track
                const currentSymbol = getTrackedPinSymbol(String(pin.id)) || '';
                setModalSymbol(currentSymbol);
                setIsEditingTrack(true);
                setModalOpen(true);
            } else {
                // Open modal to add symbol when starting to track
                setModalSymbol('');
                setIsEditingTrack(false);
                setModalOpen(true);
            }
        };
        const handleShareTextClick = async (e: any) => {
            e.preventDefault();
            e.stopPropagation();
            if (!pin.shareCode) {
                return;
            }

            try {
                await copyTrackedPinShareUrl(pin.shareCode);
                updateShareStatus(true);
            } catch (error) {
                console.error('Failed to copy share link:', error);
                updateShareStatus(false);
            }
        };
        const attachHandler = () => {
            trackTextEl = document.getElementById(trackTextId);
            if (trackTextEl) {
                trackTextEl.addEventListener('click', handleTrackTextClick);
            }

            shareTextEl = document.getElementById(shareTextId);
            if (shareTextEl) {
                shareTextEl.addEventListener('click', handleShareTextClick);
            }
        };
        const detachHandler = () => {
            if (trackTextEl) {
                trackTextEl.removeEventListener('click', handleTrackTextClick);
                trackTextEl = null;
            }

            if (shareTextEl) {
                shareTextEl.removeEventListener('click', handleShareTextClick);
                shareTextEl = null;
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
            if (shareFeedbackTimeout) {
                clearTimeout(shareFeedbackTimeout);
            }
        };
    }, [pin.id, pin.shareCode, pin.beaconID, pin.beaconName, pin.railroad, pin.subdivision, pin.milepost, pin.direction, pin.moving, pin.lastUpdate, pin.addresses, isTracked, trackColor, mapTheme, hourFormat]);

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
        <>
            <Marker
                key={pin.id}
                ref={markerRef}
                position={[pin.latitude, pin.longitude + longitudeOffset]}
                icon={createCustomIcon()}
                pane="telemetryPane"
            />
            <TrackSymbolModal
                open={modalOpen}
                currentSymbol={modalSymbol}
                onSave={handleModalSave}
                onUntrack={handleModalUntrack}
                onClose={() => setModalOpen(false)}
                theme={mapTheme}
                addresses={addressesForModal}
                showUntrackButton={isEditingTrack}
            />
        </>
    );
};

function getArrowIconSrc(direction?: string | null, moving?: boolean, mapTheme: 'dark' | 'light' = 'dark'): string {
    const suffix = mapTheme === 'dark' ? '-dark' : '-light';
    const cacheBuster = ICON_CACHE_BUSTER;

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
