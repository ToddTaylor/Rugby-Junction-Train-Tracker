import React, { useEffect, useState } from 'react';
import {
    MapContainer,
    TileLayer,
    GeoJSON,
    useMapEvents
} from 'react-leaflet';
import { LatLngTuple } from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { useSignalR } from '../hooks/useSignalR';
import { Beacon, MapPin as MapPin } from '../types/types';
import { openDB } from 'idb';
import BeaconMarkers from '../components/BeaconMarkers';
import TelemetryMarkers from '../components/TelemetryMarkers';

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
    const [beaconPins, setBeacons] = useState<Beacon[]>([]);
    const [mapPins, setMapPins] = useState<MapPin[]>([]);

    // Track loading state for each data set
    const [trackDataLoaded, setTrackDataLoaded] = useState(false);
    const [beaconsLoaded, setBeaconsLoaded] = useState(false);

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

    // Prune pins older than 15 minutes on a timer so they disappear even if no new data arrives
    // @ts-expect-error
    const [pruneTick, setPruneTick] = useState(0);
    useEffect(() => {
        const interval = setInterval(() => setPruneTick(tick => tick + 1), 30 * 1000);
        return () => clearInterval(interval);
    }, []);

    // Build a map of pins by id for TelemetryMarkers, filtering out pins older than 15 minutes
    const FIFTEEN_MINUTES = 15 * 60 * 1000;
    const now = Date.now();
    const telemetryPins: { [id: string]: MapPin } = {};
    offsetMarkers.forEach(pin => {
        const createdAt = new Date(pin.createdAt).getTime();
        if (now - createdAt <= FIFTEEN_MINUTES) {
            telemetryPins[pin.id] = pin;
        }
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

        // Always use the highest version for openDB to avoid VersionError
        const DB_VERSION = 2;

        // Get railroad data from OpenStreetMap
        const fetchRailways = async () => {
            const STORE_NAME = 'geojson';

            const db = await openDB('railways-db', DB_VERSION, {
                upgrade(db) {
                    if (!db.objectStoreNames.contains('geojson')) {
                        db.createObjectStore('geojson');
                    }
                    if (!db.objectStoreNames.contains('beacons')) {
                        db.createObjectStore('beacons');
                    }
                }
            });

            const cached = await db.get(STORE_NAME, 'railways');
            if (cached) {
                console.log('Railway map loaded from IndexedDB cache');
                setTracksData(cached);
                setTrackDataLoaded(true);
                return;
            }

            const response = await fetch('/data/overpass-wi-railways.geojson');

            if (!response.ok) throw new Error('Failed to fetch railways');

            const data = await response.json();
            await db.put(STORE_NAME, data, 'railways');

            console.log('Railway map stored in IndexedDB');

            setTracksData(data);
            setTrackDataLoaded(true);
        };

        // Get beacons from the API or cache
        const fetchBeacons = async () => {
            const STORE_NAME = 'beacons';
            const db = await openDB('railways-db', DB_VERSION, {
                upgrade(db) {
                    if (!db.objectStoreNames.contains('geojson')) {
                        db.createObjectStore('geojson');
                    }
                    if (!db.objectStoreNames.contains(STORE_NAME)) {
                        db.createObjectStore(STORE_NAME);
                    }
                }
            });

            let cached;
            try {
                cached = await db.get(STORE_NAME, 'beacons');
            } catch (e) {
                // If the store still doesn't exist, forcibly recreate DB
                await db.close();
                await indexedDB.deleteDatabase('railways-db');
                const db2 = await openDB('railways-db', DB_VERSION, {
                    upgrade(db) {
                        if (!db.objectStoreNames.contains('geojson')) {
                            db.createObjectStore('geojson');
                        }
                        if (!db.objectStoreNames.contains(STORE_NAME)) {
                            db.createObjectStore(STORE_NAME);
                        }
                    }
                });
                cached = await db2.get(STORE_NAME, 'beacons');
            }

            if (cached) {
                console.log('Beacons loaded from IndexedDB cache');
                setBeacons(cached);
                setBeaconsLoaded(true);
                return;
            }

            try {
                const apiUrl = import.meta.env.VITE_API_URL + "/api/v1/BeaconRailroads";
                const response = await fetch(apiUrl, {
                    headers: {
                        'X-Api-Key': import.meta.env.VITE_API_KEY,
                        'Content-Type': 'application/json'
                    }
                });
                if (!response.ok) throw new Error('Failed to fetch map pins');

                const { data: beacons } = await response.json();
                await db.put(STORE_NAME, beacons, 'beacons');
                setBeacons(beacons);
                setBeaconsLoaded(true);
                console.log('Beacons stored in IndexedDB');
            } catch (error) {
                console.error('Error fetching map pins:', error);
            }
        };

        // Fetch initial map telemtry pin data from the API
        const fetchInitialTelemetryPins = async () => {
            try {
                const minutesOldFilter = 15; // Fetch map pins from the last 15 minutes so old alerts are not shown
                const apiUrl = import.meta.env.VITE_API_URL + "/api/v1/MapPins?minutes=" + minutesOldFilter;
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

        // Chain loading: tracks -> beacons -> telemetry
        fetchRailways()
            .then(() => fetchBeacons())
            .then(() => fetchInitialTelemetryPins());
    }, []);

    function MapZoomListener() {
        useMapEvents({
            zoomend: (e) => setMapZoom(e.target.getZoom()),
        });
        return null;
    }

    if (!userLocation) {
        return <p>📍 Getting your location...</p>;
    }

    return (
        <MapContainer
            center={userLocation}
            zoom={mapZoom}
            style={{ height: '100%', width: '100%' }}
            scrollWheelZoom={true}
        >
            <MapZoomListener />
            <TileLayer
                url="https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png"
                attribution='&copy; <a href="https://carto.com/">CARTO</a>'
            />

            {/* Display railroad tracks using locally cached Overpass query of just WI and then generate GeoJSON file. */}
            {trackData && <GeoJSON data={trackData} style={{ color: '#005aa9', weight: 2 }} />}

            {/* Beacon markers */}
            {trackDataLoaded && <BeaconMarkers pins={beaconPins} zoom={mapZoom} />}

            {/* Telemetry markers */}
            {trackDataLoaded && beaconsLoaded && <TelemetryMarkers pins={telemetryPins} zoom={mapZoom} />}

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
