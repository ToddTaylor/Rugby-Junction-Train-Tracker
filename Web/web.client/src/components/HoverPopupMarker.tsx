import { Marker } from 'react-leaflet';
import L from 'leaflet';
import { useEffect, useRef } from 'react';
import ArrowMapPin from './ArrowMapPin';
import ReactDOMServer from 'react-dom/server';
import { MapPin } from '../types/types';
import { parseISO } from 'date-fns/parseISO';
import { format } from 'date-fns';  
function HoverPopupMarker({ pin }: { pin: MapPin }) {
    const markerRef = useRef<L.Marker>(null);

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

        return () => {
            marker.off('mouseover');
            marker.off('mouseout');
        };
    }, [pin]);

    const createCustomIcon = (direction?: string, moving?: Boolean) =>
        L.divIcon({
            html: ReactDOMServer.renderToString(<ArrowMapPin direction={direction as any} moving={moving as any} />),
            className: '',
            iconSize: [20, 20],
            iconAnchor: [10, 0],
        });

    return (
        <Marker
            ref={markerRef}
            position={[pin.latitude, pin.longitude]}
            icon={createCustomIcon(pin.direction, pin.moving)}
        />
    );
}

export default HoverPopupMarker;
