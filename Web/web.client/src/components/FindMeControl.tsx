import { useEffect, useRef } from 'react';
import L from 'leaflet';
import type { Map as LeafletMap } from 'leaflet';

interface FindMeControlProps {
    mapRef: React.RefObject<LeafletMap | null>;
    userLocation: { latitude: number; longitude: number } | null;
}

const FindMeControl: React.FC<FindMeControlProps> = ({ mapRef, userLocation }) => {
    const controlRef = useRef<L.Control | null>(null);

    useEffect(() => {
        if (!mapRef.current) return;

        const map = mapRef.current;

        // Remove existing control if any
        if (controlRef.current && map.hasLayer(controlRef.current as any)) {
            map.removeControl(controlRef.current);
        }

        // Only create control if user location is available
        if (!userLocation) {
            return;
        }

        // Create custom control
        const FindMeControlClass = L.Control.extend({
            onAdd: (_map: LeafletMap) => {
                const container = L.DomUtil.create('div', 'leaflet-bar leaflet-control find-me-control');
                const button = L.DomUtil.create('button', 'find-me-button', container);
                
                // Add SVG icon
                button.innerHTML = `
                    <svg class="find-me-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                        <circle cx="12" cy="12" r="9" />
                        <circle cx="12" cy="12" r="3" />
                        <path d="M12 1v6M12 17v6M1 12h6M17 12h6" />
                    </svg>
                `;
                
                button.title = 'Center map on your location';
                
                // Prevent map interaction when clicking button
                L.DomEvent.disableClickPropagation(button);
                L.DomEvent.disableScrollPropagation(button);
                
                // Handle click
                button.addEventListener('click', () => {
                    if (userLocation && mapRef.current) {
                        // Get zoom from localStorage or use default
                        const savedMapState = JSON.parse(localStorage.getItem('mapState') || 'null');
                        const zoomLevel = savedMapState?.zoom || 14;
                        
                        mapRef.current.setView(
                            [userLocation.latitude, userLocation.longitude],
                            zoomLevel
                        );
                    }
                });
                
                return container;
            }
        });

        const findMeControl = new FindMeControlClass({ position: 'topleft' });
        findMeControl.addTo(map);
        controlRef.current = findMeControl;

        return () => {
            if (controlRef.current && map.hasLayer(controlRef.current as any)) {
                map.removeControl(controlRef.current);
                controlRef.current = null;
            }
        };
    }, [mapRef, userLocation]);

    return null;
};

export default FindMeControl;
