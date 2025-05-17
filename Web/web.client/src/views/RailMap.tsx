import React, { useEffect, useState } from 'react';
import {
    MapContainer,
    TileLayer,
    GeoJSON,
} from 'react-leaflet';
import { LatLngTuple } from 'leaflet';
import 'leaflet/dist/leaflet.css';
import HoverPopupMarker from '../components/HoverPopupMarker';
import { useSignalR } from '../hooks/useSignalR';
import { MapPin as MapPin } from '../types/types';
import { openDB } from 'idb';

const fallbackCenter: LatLngTuple = [37.5, -122]; // Default if location fails

function metersToLongitudeDegrees(meters: number, latitude: number): number {
    // 1 deg longitude = 111320*cos(latitude)
    return meters / (111320 * Math.cos(latitude * Math.PI / 180));
}

// Helper: convert pixels to meters at a given latitude and zoom
function pixelsToMeters(pixels: number, latitude: number, zoom: number): number {
    // Earth's circumference at equator: 40075016.686 meters
    // 256 px = 40075016.686 meters at zoom 0
    // metersPerPixel = circumference * cos(latitude) / (2 ^ (zoom + 8))
    const metersPerPixel = (40075016.686 * Math.cos(latitude * Math.PI / 180)) / Math.pow(2, zoom + 8);
    return pixels * metersPerPixel;
}

const RailMap: React.FC = () => {
    const [userLocation, setUserLocation] = useState<LatLngTuple | null>(null);
    const [mapZoom, setMapZoom] = useState<number>(11);

    const [trackData, setTracksData] = useState<GeoJSON.GeoJsonObject | null>(null);
    const [mapPins, setMapPins] = useState<MapPin[]>([]);

    useSignalR((pin: MapPin) => {
        setMapPins(prev => updateAlerts(prev, pin));
    });

    const sortedData: MapPin[] = Array.from(mapPins.values())
        .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
        .map((pin, index) => ({
            ...pin,
            id: `row-${index + 1}`,
        }));

    // Group pins by their lat/lng to handle overlapping markers
    const groupedPins: { [key: string]: MapPin[] } = {};
    sortedData.forEach(pin => {
        const key = `${pin.latitude},${pin.longitude}`;
        if (!groupedPins[key]) groupedPins[key] = [];
        groupedPins[key].push(pin);
    });

    // Marker icon size in pixels
    const MARKER_SIZE_PX = 20;

    // Offset markers so that pins with the same lat/lng are side by side (east-west)
    const offsetMarkers: MapPin[] = [];
    Object.values(groupedPins).forEach(group => {
        const n = group.length;
        group.forEach((pin, idx) => {
            if (n === 1) {
                offsetMarkers.push(pin);
            } else {
                // Center the group around the original longitude
                const offsetIndex = idx - (n - 1) / 2;
                // Convert marker width in pixels to meters, then to longitude degrees
                const offsetMeters = pixelsToMeters(MARKER_SIZE_PX, pin.latitude, mapZoom);
                const offsetDeg = metersToLongitudeDegrees(offsetMeters * offsetIndex, pin.latitude);
                offsetMarkers.push({
                    ...pin,
                    longitude: pin.longitude + offsetDeg
                });
            }
        });
    });

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
                console.log('Railway map loaded from IndexedDB cache');
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

        // Fetch initial map alerts from the API
        const fetchInitialAlerts = async () => {
            try {
                const apiUrl = import.meta.env.VITE_API_URL + "/api/v1/MapPins";
                const response = await fetch(apiUrl, {
                    headers: {
                        'X-Api-Key': import.meta.env.VITE_API_KEY,  
                        'Content-Type': 'application/json' 
                    }
                });
                if (!response.ok) throw new Error('Failed to fetch map pins');

                const { data: pins } = await response.json();
                setMapPins(pins);
            } catch (error) {
                console.error('Error fetching map pins:', error);
            }
        };

        fetchRailways();
        fetchInitialAlerts();
    }, []);

    if (!userLocation) {
        return <p>📍 Getting your location...</p>;
    }

    return (
        <MapContainer
            center={userLocation}
            zoom={mapZoom}
            style={{ height: '100%', width: '100%' }}
            scrollWheelZoom={true}
            whenReady={event => {
                setMapZoom(event.target.getZoom());
                event.target.on('zoomend', () => setMapZoom(event.target.getZoom()));
            }}
        >
            <TileLayer
                url="https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png"
                attribution='&copy; <a href="https://carto.com/">CARTO</a>'
            />

            {/* Display railroad tracks using locally cached Overpass query of just WI and then generate GeoJSON file. */}
            {trackData && <GeoJSON data={trackData} style={{ color: '#005aa9', weight: 2 }} />}

            {/* Display railroad tracks using OpenRailwayMap. Quite fast, but occasionally shows warning from too many queries... */}
            {/*<TileLayer*/}
            {/*    url="https://{s}.tiles.openrailwaymap.org/standard/{z}/{x}/{y}.png"*/}
            {/*    attribution='&copy; <a href="https://www.openrailwaymap.org/">OpenRailwayMap</a> contributors'*/}
            {/*    opacity={0.8}*/}
            {/*/>*/}

            {offsetMarkers && offsetMarkers.map((pin: MapPin) => (
                <HoverPopupMarker key={pin.id} pin={pin} />
            ))}

        </MapContainer>
    );
};

/**
 * TODO: Unit test this function.
 */
function updateAlerts(pins: MapPin[], newPin: MapPin): MapPin[] {
    const existingIndex = pins.findIndex(
        (alert) => alert.addressID === newPin.addressID
    );

    if (existingIndex !== -1) {
        // Remove the existing alert with the same addressID
        pins.splice(existingIndex, 1);
    }

    // Add the new pin to the array
    return [...pins, newPin];
}

export default RailMap;
