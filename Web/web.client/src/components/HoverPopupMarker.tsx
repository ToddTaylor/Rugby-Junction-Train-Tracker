import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { useEffect, useRef } from 'react';
import DirectionIcon from './DirectionIcon';
import ReactDOMServer from 'react-dom/server';
import { MapPin } from '../types/types';

function HoverPopupMarker({ pin }: { pin: MapPin }) {
    const markerRef = useRef<L.Marker>(null);

    useEffect(() => {
        const marker = markerRef.current;
        if (!marker) return;

        const popupContent = `
      <strong>Train ID:</strong> ${pin.addressID}<br/>
      <strong>Direction:</strong> ${pin.direction || 'Unknown'}<br/>
      <strong>Source:</strong> ${pin.source}<br/>
      <strong>Moving:</strong> ${pin.moving || 'Unknown'}<br/>
      <strong>Timestamp:</strong> ${new Date(pin.createdAt).toLocaleString()}
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
    }, [pin]);

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
            position={[pin.latitude, pin.longitude]}
            icon={createCustomIcon(pin.direction)}
        />
    );
}

export default HoverPopupMarker;
