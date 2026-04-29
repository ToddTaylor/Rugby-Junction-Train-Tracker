import { useEffect, useRef } from 'react';
import { LatLngTuple, Map as LeafletMap } from 'leaflet';
import L from 'leaflet';
import { MilepostPoint } from '../hooks/useMileposts';

type MilepostLayerProps = {
    mapRef: React.RefObject<LeafletMap | null>;
    points: MilepostPoint[];
    mapZoom: number;
    mapCenter: LatLngTuple;
    mapTheme: 'dark' | 'light';
    minZoom: number;
};

const MilepostLayer: React.FC<MilepostLayerProps> = ({
    mapRef,
    points,
    mapZoom,
    mapCenter,
    mapTheme,
    minZoom
}) => {
    const milepostLayerRef = useRef<L.LayerGroup | null>(null);

    useEffect(() => {
        return () => {
            if (milepostLayerRef.current && mapRef.current) {
                mapRef.current.removeLayer(milepostLayerRef.current);
                milepostLayerRef.current = null;
            }
        };
    }, [mapRef]);

    useEffect(() => {
        if (!mapRef.current) return;

        const map = mapRef.current;
        const pane = map.getPane('milepostPane');
        if (!pane) {
            map.createPane('milepostPane');
            const createdPane = map.getPane('milepostPane');
            if (createdPane) {
                createdPane.style.zIndex = '425';
            }
        }

        if (!milepostLayerRef.current) {
            milepostLayerRef.current = L.layerGroup().addTo(map);
        }

        const layer = milepostLayerRef.current;
        layer.clearLayers();

        if (mapZoom < minZoom || points.length === 0) {
            return;
        }

        const bounds = map.getBounds().pad(0.2);
        points.forEach(point => {
            if (!bounds.contains([point.lat, point.lng])) return;
            const icon = L.divIcon({
                className: 'milepost-label-icon',
                html: `<span class="milepost-label milepost-label--${mapTheme}">${point.milepost}</span>`,
                iconAnchor: [0, 0]
            });
            L.marker([point.lat, point.lng], {
                icon,
                pane: 'milepostPane',
                interactive: false,
                keyboard: false
            }).addTo(layer);
        });
    }, [points, mapZoom, mapCenter, mapTheme, minZoom, mapRef]);

    return null;
};

export default MilepostLayer;
