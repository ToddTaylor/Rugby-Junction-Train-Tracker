import { Marker, useMap } from 'react-leaflet';
import L from 'leaflet';
import { useEffect, useRef } from 'react';
import DirectionIcon from './DirectionIcon';
import ReactDOMServer from 'react-dom/server';
import { MapAlert } from '../types/types';

function HoverPopupMarker({ alert }: { alert: MapAlert }) {
    const markerRef = useRef<L.Marker>(null);
    const map = useMap(); // Needed for leaflet context

    useEffect(() => {
        const marker = markerRef.current;
        if (!marker) return;

        const popupContent = `
      <strong>Train ID:</strong> ${alert.addressID}<br/>
      <strong>Direction:</strong> ${alert.direction || 'Unknown'}<br/>
      <strong>Source:</strong> ${alert.source}<br/>
      <strong>Moving:</strong> ${alert.moving || 'Unknown'}<br/>
      <strong>Timestamp:</strong> ${new Date(alert.timestamp).toLocaleString()}
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
    }, [alert]);

    const createCustomIcon = (direction?: string) =>
        L.divIcon({
            html: ReactDOMServer.renderToString(<DirectionIcon direction={direction as any} />),
            className: '',
            iconSize: [20, 20],
            iconAnchor: [10, 0],
        });

    return (
        <Marker
            ref={markerRef}
            position={[alert.latitude, alert.longitude]}
            icon={createCustomIcon(alert.direction)}
        />
    );
}

export default HoverPopupMarker;
