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
import { useSignalR } from '../hooks/useSignalR';
import { Alert } from '../types/types';
import hash from 'object-hash';
import axios from 'axios';
import osm2geojson from 'osm2geojson-lite';

// Source of railroad data: https://geodata.bts.gov/datasets/usdot::north-american-rail-network-lines-class-i-freight-railroads-view/about
//const RAILROAD_URL = 'https://services.arcgis.com/xOi1kZaI0eWDREZv/arcgis/rest/services/NTAD_North_American_Rail_Network_Lines_Class_I_Railroads/FeatureServer/0/query?outFields=*&where=1%3D1&f=geojson';

const fallbackCenter: LatLngTuple = [37.5, -122]; // Default if location fails

const RailMap: React.FC = () => {
    const [userLocation, setUserLocation] = useState<LatLngTuple | null>(null);

    const [trackData, setTracksData] = useState<GeoJSON.GeoJsonObject | null>(null);
    const overpassQuery = `[out:json][timeout:25];
    (
      way["railway"="rail"](42.49,-92.89,47.31,-86.25);
    );
    out body;
    >;
    out skel qt;`;

    const [milepostsData, setMilepostsData] = useState<GeoJSON.GeoJsonObject | null>(null);
    const [alerts, setAlerts] = useState<Alert[]>([]);

    useSignalR((alert: Alert) => {
        setAlerts(prev => updateAlerts(prev, alert));
    });

    const sortedData: Alert[] = Array.from(alerts.values())
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
        // End browser location

        // Get railroad data from OpenStreetMap
        const fetchRailways = async () => {
            try {
                const response = await axios.get(
                    'https://overpass-api.de/api/interpreter',
                    {
                        params: { data: overpassQuery },
                    }
                );

                // Convert Overpass JSON to GeoJSON
                const geojson = osm2geojson(response.data);
                setTracksData(geojson);
            } catch (error) {
                console.error('Error fetching railway data:', error);
            }
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
            iconSize: [40, 40],
            iconAnchor: [20, 0],
        });

    const onMilepostFeature = (feature: any, layer: L.Layer) => {
        if (feature.properties) {
            layer.bindPopup(`Milepost: ${feature.properties.milepost}`);
        }
    };

    const getMilepostStyle = () => {
        return {
            radius: 6,
            fillColor: 'white',
            color: 'white',
            weight: 1,
            opacity: 1,
            fillOpacity: 0.9,
        };
    };

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

            {/* Display railroad tracks using Overpass Query of just WI and then generate GeoJSON file.  
                Ask ChatGPT how to export the GeoJSON file to make it local as online loading takes time.
                This approach avoids the OpenRailwayMap alert when using too many queries, but map is 
                less detailed. */}
            {trackData && <GeoJSON data={trackData} style={{ color: 'white', weight: 2 }} />}

            {/* Display railroad tracks using OpenRailwayMap. Quite fast, but occasionally shows warning from too many queries... */}
            {/*<TileLayer*/}
            {/*    url="https://{s}.tiles.openrailwaymap.org/standard/{z}/{x}/{y}.png"*/}
            {/*    attribution='&copy; <a href="https://www.openrailwaymap.org/">OpenRailwayMap</a> contributors'*/}
            {/*    opacity={0.8}*/}
            {/*/>*/}

            {/* Display railroad tracks using local GeoJSON file.  Is quite slow... */}
            {/*{trackData && <GeoJSON data={trackData} />}*/}

            {milepostsData && (
                <GeoJSON
                    key={hash(milepostsData)}
                    data={milepostsData}
                    onEachFeature={onMilepostFeature}
                    pointToLayer={(_, latlng) => {
                        // Assign a stable index per point
                        const style = getMilepostStyle();
                        return L.circleMarker(latlng, style);
                    }}
                />
            )}

            {sortedData && sortedData.map((alert: Alert) => (
                <Marker
                    key={alert.id}
                    position={[alert.latitude, alert.longitude]}
                    icon={createCustomIcon(alert.direction)}
                >
                    <Popup>
                        <strong>Train ID:</strong> {alert.addressID}<br />
                        <strong>Direction:</strong> {alert.direction || 'Unknown'}<br />
                        <strong>Source:</strong> {alert.source}<br />
                        <strong>Moving:</strong> {alert.moving || 'Unknown'}<br />
                        <strong>Timestamp:</strong> {new Date(alert.timestamp as string).toLocaleString()}
                    </Popup>
                </Marker>
            ))}

        </MapContainer>
    );
};

function updateAlerts(alerts: Alert[], newAlert: Alert): Alert[] {
    const existingIndex = alerts.findIndex(
        (alert) =>
            //alert.addressID === newAlert.addressID &&
            alert.latitude === newAlert.latitude &&
            alert.longitude === newAlert.longitude
    );

    if (existingIndex === -1) {
        // No existing alert with the same addressID, just add it
        return [...alerts, newAlert];
    }

    // Replace only if the new alert is more recent
    if (newAlert.timestamp > alerts[existingIndex].timestamp) {
        const updatedAlerts = [...alerts];
        updatedAlerts[existingIndex] = newAlert;
        return updatedAlerts;
    }

    // Otherwise, keep the current alerts unchanged
    return alerts;
}

export default RailMap;
