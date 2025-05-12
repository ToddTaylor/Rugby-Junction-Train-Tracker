import React, { useEffect, useState } from 'react';
import ReactDOMServer from 'react-dom/server';
import {
    MapContainer,
    Marker,
    Popup,
    TileLayer,
    GeoJSON,
} from 'react-leaflet';
import { LatLngTuple } from 'leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import DirectionIcon from '../components/DirectionIcon';
import HoverPopupMarker from '../components/HoverPopupMarker';
import { useSignalR } from '../hooks/useSignalR';
import { MapAlert } from '../types/types';
import hash from 'object-hash';
import { openDB } from 'idb';

// Source of railroad data: https://geodata.bts.gov/datasets/usdot::north-american-rail-network-lines-class-i-freight-railroads-view/about
//const RAILROAD_URL = 'https://services.arcgis.com/xOi1kZaI0eWDREZv/arcgis/rest/services/NTAD_North_American_Rail_Network_Lines_Class_I_Railroads/FeatureServer/0/query?outFields=*&where=1%3D1&f=geojson';

const fallbackCenter: LatLngTuple = [37.5, -122]; // Default if location fails

const RailMap: React.FC = () => {
    const [userLocation, setUserLocation] = useState<LatLngTuple | null>(null);

    const [trackData, setTracksData] = useState<GeoJSON.GeoJsonObject | null>(null);
    const [milepostsData, setMilepostsData] = useState<GeoJSON.GeoJsonObject | null>(null);
    const [mapAlerts, setMapAlerts] = useState<MapAlert[]>([]);

    useSignalR((alert: MapAlert) => {
        setMapAlerts(prev => updateAlerts(prev, alert));
    });

    const sortedData: MapAlert[] = Array.from(mapAlerts.values())
        .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
        .map((alert, index) => ({
            ...alert,
            id: `row-${index + 1}`,
        }));

    useEffect(() => {
        // Get browser location
        navigator.geolocation.getCurrentPosition(
            (position) => {
                const coords: LatLngTuple = [position.coords.latitude, position.coords.longitude];
                setUserLocation(coords);
            },
            (error) => {
                console.warn('Geolocation error:', error);
                setUserLocation(fallbackCenter);
            }
        );

        // Get railroad data from OpenStreetMap
        const fetchRailways = async () => {
            const STORE_NAME = 'geojson';

            const db = await openDB('railways-db', 1, {
                upgrade(db) {
                    db.createObjectStore(STORE_NAME);
                }
            });

            const cached = await db.get(STORE_NAME, 'railways');
            if (cached) {
                console.log('Railway map loaded from IndexedDB');
                setTracksData(cached);
                return;
            }

            const response = await fetch('/data/overpass-wi-railways.geojson');

            if (!response.ok) throw new Error('Failed to fetch railways');

            const data = await response.json();
            await db.put(STORE_NAME, data, 'railways');

            console.log('Railway map stored in IndexedDB');

            setTracksData(data);
        };

        fetchRailways();

        // End railroad data from OpenStreetMap

        //fetch("/data/railroads.geojson")
        //    .then((res) => res.json())
        //    .then((geojson) => {
        //        const filtered = {
        //            ...geojson,
        //            features: geojson.features.filter(
        //                (feature: any) => feature.properties.STATEAB === 'WI'
        //            ),
        //        };
        //        setTracksData(filtered);
        //    })
        //    .catch((err) => console.error('Failed to load railroads:', err));

        fetch("/data/mileposts.geojson")
            .then((res) => res.json())
            .then((geojson) => {
                setMilepostsData(geojson);
            })
            .catch((err) => console.error('Failed to load mileposts:', err));
    }, []);

    const createCustomIcon = (direction?: string) =>
        L.divIcon({
            html: ReactDOMServer.renderToString(<DirectionIcon direction={direction as any} />),
            className: '',
            iconSize: [20, 20],
            iconAnchor: [10, 0],
        });

    //const onMilepostFeature = (feature: any, layer: L.Layer) => {
    //    if (feature.properties) {
    //        layer.bindPopup(`Milepost: ${feature.properties.milepost}`);
    //    }
    //};

    //const getMilepostStyle = () => {
    //    return {
    //        radius: 6,
    //        fillColor: 'white',
    //        color: 'white',
    //        weight: 1,
    //        opacity: 1,
    //        fillOpacity: 0.9,
    //    };
    //};

    if (!userLocation) {
        return <p>📍 Getting your location...</p>;
    }

    return (
        <MapContainer
            center={userLocation}
            zoom={11}
            style={{ height: '100%', width: '100%' }}
            scrollWheelZoom={true}
        >
            <TileLayer
                url="https://tiles.stadiamaps.com/tiles/alidade_smooth_dark/{z}/{x}/{y}{r}.png"
                attribution='&copy; <a href="https://stadiamaps.com/">Stadia Maps</a>, &copy; <a href="https://openmaptiles.org/">OpenMapTiles</a>'
            />

            {/* Display railroad tracks using locally cached Overpass query of just WI and then generate GeoJSON file. */}
            {trackData && <GeoJSON data={trackData} style={{ color: '#005aa9', weight: 2 }} />}

            {/* Display railroad tracks using OpenRailwayMap. Quite fast, but occasionally shows warning from too many queries... */}
            {/*<TileLayer*/}
            {/*    url="https://{s}.tiles.openrailwaymap.org/standard/{z}/{x}/{y}.png"*/}
            {/*    attribution='&copy; <a href="https://www.openrailwaymap.org/">OpenRailwayMap</a> contributors'*/}
            {/*    opacity={0.8}*/}
            {/*/>*/}

            {/* Display railroad tracks using local GeoJSON file.  Is quite slow... */}
            {/*{trackData && <GeoJSON data={trackData} />}*/}

            {/*{milepostsData && (*/}
            {/*    <GeoJSON*/}
            {/*        key={hash(milepostsData)}*/}
            {/*        data={milepostsData}*/}
            {/*        onEachFeature={onMilepostFeature}*/}
            {/*        pointToLayer={(_, latlng) => {*/}
            {/*            // Assign a stable index per point*/}
            {/*            const style = getMilepostStyle();*/}
            {/*            return L.circleMarker(latlng, style);*/}
            {/*        }}*/}
            {/*    />*/}
            {/*)}*/}

            {sortedData && sortedData.map((alert: MapAlert) => (
                <HoverPopupMarker alert={alert} />
            ))}

        </MapContainer>
    );
};

/**
 * TODO: Unit test this function.
 */
function updateAlerts(alerts: MapAlert[], newAlert: MapAlert): MapAlert[] {
    const existingIndex = alerts.findIndex(
        (alert) => alert.addressID === newAlert.addressID
    );

    if (existingIndex !== -1) {
        // Remove the existing alert with the same addressID
        alerts.splice(existingIndex, 1);
    }

    // Add the new alert to the array
    return [...alerts, newAlert];
}

export default RailMap;
