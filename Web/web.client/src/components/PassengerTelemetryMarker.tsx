import { useEffect, useRef } from 'react';
import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { PassengerMapPin } from '../types/PassengerMapPin';

const ICON_CACHE_BUSTER = import.meta.env.VITE_APP_VERSION
  ? `?v=${import.meta.env.VITE_APP_VERSION}`
  : '';

type Props = {
  pin: PassengerMapPin;
  size: number;
  mapTheme: 'dark' | 'light';
};

const PassengerTelemetryMarker: React.FC<Props> = ({ pin, size, mapTheme }) => {
  const markerRef = useRef<L.Marker>(null);

  // Inject the same dark-mode popup CSS used by TelemetryMarker so theming is consistent.
  useEffect(() => {
    const styleId = 'leaflet-popup-darkmode-style';
    if (!document.getElementById(styleId)) {
      const style = document.createElement('style');
      style.id = styleId;
      style.innerHTML = `
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

  useEffect(() => {
    const marker = markerRef.current;
    if (!marker) return;

    const staleColor = mapTheme === 'dark' ? '#fbbf24' : '#d97706';

    const localTime = (() => {
      try {
        return new Date(pin.updatedAt).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', hour12: true });
      } catch {
        return pin.updatedAt;
      }
    })();

    // Match the same plain-text + <br/> structure used by TelemetryMarker.
    // No inline background or text color — the injected CSS handles dark/light theming.
    const popupContent = `
      <div>
        <strong>${pin.provider}</strong><br/>
        ${pin.trainNum} ${pin.routeName}<br/>
        ${pin.heading}<br/>
        ${localTime}<br/>
        ${pin.velocity} MPH${pin.isStale ? `<br/><span style='color:${staleColor};font-weight:600;'>⚠ Stale data</span>` : ''}
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

    return () => {
      marker.off('click', handleMarkerClick);
    };
  }, [pin, mapTheme]);

  return (
    <Marker
      ref={markerRef}
      position={[pin.latitude, pin.longitude]}
      icon={createPassengerIcon(pin.heading, pin.velocity > 0, size)}
      pane="telemetryPane"
    />
  );
};

function createPassengerIcon(heading: string, moving: boolean, size: number): L.DivIcon {
  const iconSrc = getPassengerIconSrc(heading, moving);
  const rotation = getRotationFromHeading(heading);

  return L.divIcon({
    html: `
      <div style="
        width:${size}px;
        height:${size}px;
        display:flex;
        align-items:center;
        justify-content:center;
      ">
        <img
          src="${iconSrc}"
          alt="Passenger train"
          style="
            width:${size}px;
            height:${size}px;
            transform: rotate(${rotation}deg);
            border-radius:50%;
            box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.85);
          "
        />
      </div>
    `,
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
    popupAnchor: [0, -size / 2],
    className: 'telemetry-marker-z',
  });
}

function getPassengerIconSrc(heading: string, moving: boolean): string {
  const normalized = (heading || '').toUpperCase();
  if (!normalized || normalized === 'UNKNOWN') {
    return moving
      ? `/icons/passenger-unknown-moving.svg${ICON_CACHE_BUSTER}`
      : `/icons/passenger-unknown-stationary.svg${ICON_CACHE_BUSTER}`;
  }

  return moving
    ? `/icons/passenger-arrow-moving.svg${ICON_CACHE_BUSTER}`
    : `/icons/passenger-arrow-stationary.svg${ICON_CACHE_BUSTER}`;
}

function getRotationFromHeading(heading: string): number {
  switch ((heading || '').toUpperCase()) {
    case 'NORTHBOUND': return 0;
    case 'NORTHEASTBOUND': return 45;
    case 'EASTBOUND': return 90;
    case 'SOUTHEASTBOUND': return 135;
    case 'SOUTHBOUND': return 180;
    case 'SOUTHWESTBOUND': return 225;
    case 'WESTBOUND': return 270;
    case 'NORTHWESTBOUND': return 315;
    default: return 0;
  }
}

export default PassengerTelemetryMarker;