import L from 'leaflet';
import ReactDOMServer from 'react-dom/server';
import { ArrowIcon } from './ArrowIcon';
import { UnknownIcon } from './UnknownIcon';

const ICON_CACHE_BUSTER = import.meta.env.VITE_APP_VERSION
    ? `?v=${import.meta.env.VITE_APP_VERSION}`
    : '';

/**
 * The divIcon for a train: an arrow rotated to its direction, or a question mark when the direction is unknown,
 * tinted by movement and faded by age.
 */
export function createTelemetryIcon(
    direction: string | null | undefined,
    moving: boolean | undefined,
    isLocal: boolean | undefined,
    mapTheme: 'dark' | 'light',
    brightness: number,
    trackColor: string | undefined,
    size: number
): L.DivIcon {
    const iconSrc = getArrowIconSrc(direction, moving, mapTheme);
    const html = direction
        ? ReactDOMServer.renderToString(
            <ArrowIcon
                iconSrc={iconSrc}
                brightness={brightness}
                trackColor={trackColor}
                size={size}
                rotation={getRotation(direction)}
                isLocal={isLocal}
            />
        )
        : ReactDOMServer.renderToString(
            <UnknownIcon
                iconSrc={iconSrc}
                brightness={brightness}
                trackColor={trackColor}
                size={size}
                isLocal={isLocal}
            />
        );

    return L.divIcon({
        html,
        iconSize: [size, size],
        iconAnchor: [size / 2, size / 2], // absolute center of marker
        popupAnchor: [0, -size / 2],      // popup appears above the marker, scales with size
        className: 'telemetry-marker-z',
    });
}

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

