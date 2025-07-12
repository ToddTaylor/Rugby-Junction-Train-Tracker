import React, { useEffect, useState, useRef } from 'react';
import {
    MapContainer,
    TileLayer,
    GeoJSON,
    useMapEvents
} from 'react-leaflet';
import { LatLngTuple, Map as LeafletMap } from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { useSignalR } from '../hooks/useSignalR';
import { Beacon, MapPin as MapPin } from '../types/types';
import { openDB } from 'idb';
import BeaconMarkers from '../components/BeaconMarkers';
import TelemetryMarkers from '../components/TelemetryMarkers';
import { getTrackedMapPins } from '../components/trackUtils'; // adjust path as needed

const fallbackCenter: LatLngTuple = [44.524570, -89.567290]; // Default if location fails

// Helper to get cached location from localStorage
function getCachedLocation(): LatLngTuple | null {
    const cached = localStorage.getItem('cachedUserLocation');
    if (cached) {
        try {
            const [lat, lng] = JSON.parse(cached);
            if (typeof lat === 'number' && typeof lng === 'number') {
                return [lat, lng];
            }
        } catch {
            // Ignore parse errors
        }
    }
    return null;
}

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

// Always use the highest version for openDB to avoid VersionError
const DB_VERSION = 2;

// Get railroad data from OpenStreetMap
const fetchRailways = async (setTracksData: any, setTrackDataLoaded: any) => {
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
const fetchBeacons = async (setBeacons: any, setBeaconsLoaded: any) => {
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

// Fetch initial map telemetry pin data from the API
const fetchInitialTelemetryPins = async (setMapPins: any) => {
    try {
        const minutesOldFilter = 15; // Fetch map pins from the last 15 minutes so old map pins are not shown
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

const RailMap: React.FC = () => {
    // Use cached location if available, else fallbackCenter
    const cachedLocation = getCachedLocation();
    const [userLocation, setUserLocation] = useState<LatLngTuple | null>(
        cachedLocation || fallbackCenter
    );
    // Set zoom to 11 if cached location exists, else 7
    const [mapZoom, setMapZoom] = useState<number>(cachedLocation ? 11 : 7);

    const [trackData, setTracksData] = useState<GeoJSON.GeoJsonObject | null>(null);
    const [beaconPins, setBeacons] = useState<Beacon[]>([]);
    const [mapPins, setMapPins] = useState<MapPin[]>([]);

    // Track loading state for each data set
    const [trackDataLoaded, setTrackDataLoaded] = useState(false);
    const [beaconsLoaded, setBeaconsLoaded] = useState(false);

    useSignalR({
        MapPinUpdate: (mapPin: MapPin) => {
            setMapPins(mapPins => updateMapPins(mapPins, mapPin));
        },
        BeaconUpdate: (beacon: Beacon) => {
            setBeacons(beacons => updateBeacon(beacons, beacon));  
        }
    });

    const sortedData: MapPin[] = mapPins
        .sort((a, b) => new Date(b.lastUpdate).getTime() - new Date(a.lastUpdate).getTime())
        .map((pin) => ({
            ...pin,
            id: pin.id,
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
        if (n === 1) {
            // Only one marker at this location, do not offset
            offsetMarkers.push({
                ...group[0],
                longitude: group[0].longitude // ensure no offset
            });
        } else {
            // As markers are removed, recalculate offsets so remaining markers are always centered
            group.forEach((pin, idx) => {
                // Center the group around the original longitude
                const offsetIndex = idx - (n - 1) / 2;
                // Convert marker width in pixels to meters, then to longitude degrees
                const offsetMeters = pixelsToMeters(MARKER_SIZE_PX, pin.latitude, mapZoom);
                const offsetDeg = metersToLongitudeDegrees(offsetMeters * offsetIndex, pin.latitude);
                offsetMarkers.push({
                    ...pin,
                    longitude: pin.longitude + offsetDeg
                });
            });
        }
    });

    // Prune pins older than 10 minutes on a timer so they disappear even if no new data arrives
    // @ts-expect-error
    const [pruneTick, setPruneTick] = useState(0);
    useEffect(() => {
        const interval = setInterval(() => setPruneTick(tick => tick + 1), 30 * 1000);
        return () => clearInterval(interval);
    }, []);

    // Build a filtered list of pins that are not older than 10 minutes,
    // but change pins where any address source == "EOT" to half the time (5 minutes)
    // For filtering (milliseconds)
    const MAX_PIN_AGE_MINUTES = Number(import.meta.env.VITE_MAX_PIN_AGE_MINUTES);
    const MAX_PIN_AGE_MS = MAX_PIN_AGE_MINUTES * 60 * 1000;
    const now = Date.now();
    const filteredPins: MapPin[] = [];
    Object.values(groupedPins).forEach(group => {
        group.forEach(pin => {
            const lastUpdate = new Date(pin.lastUpdate).getTime();
            // If any address has source "EOT", cut the max age in half.
            const hasEOT = Array.isArray(pin.addresses) && pin.addresses.some(addr => addr.source === "EOT");
            const maxAge = hasEOT ? MAX_PIN_AGE_MS / 2 : MAX_PIN_AGE_MS;
            if (now - lastUpdate <= maxAge) {
                filteredPins.push(pin);
            }
        });
    });

    // Group filtered pins by their lat/lng to handle overlapping markers
    const groupedFilteredPins: { [key: string]: MapPin[] } = {};
    filteredPins.forEach(pin => {
        const key = `${pin.latitude},${pin.longitude}`;
        if (!groupedFilteredPins[key]) groupedFilteredPins[key] = [];
        groupedFilteredPins[key].push(pin);
    });

    // Re-apply offset logic to only the filtered pins
    const offsetFilteredMarkers: MapPin[] = [];
    Object.values(groupedFilteredPins).forEach(group => {
        const n = group.length;
        if (n === 1) {
            offsetFilteredMarkers.push({
                ...group[0],
                longitude: group[0].longitude
            });
        } else {
            group.forEach((pin, idx) => {
                const offsetIndex = idx - (n - 1) / 2;
                const offsetMeters = pixelsToMeters(MARKER_SIZE_PX, pin.latitude, mapZoom);
                const offsetDeg = metersToLongitudeDegrees(offsetMeters * offsetIndex, pin.latitude);
                offsetFilteredMarkers.push({
                    ...pin,
                    longitude: pin.longitude + offsetDeg
                });
            });
        }
    });

    // Build a map of pins by id for TelemetryMarkers
    const telemetryPins: { [id: string]: MapPin } = {};
    offsetFilteredMarkers.forEach(pin => {
        telemetryPins[pin.id] = pin;
    });

    // Use ref to get the map instance
    const mapRef = useRef<LeafletMap | null>(null);

    useEffect(() => {
        // Get browser location
        navigator.geolocation.getCurrentPosition(
            (position) => {
                const coords: LatLngTuple = [position.coords.latitude, position.coords.longitude];
                setUserLocation(coords);
                // Cache the location in localStorage
                localStorage.setItem('cachedUserLocation', JSON.stringify(coords));
            },
            (error) => {
                console.warn('Geolocation error:', error);
                // Only use fallbackCenter if there is no cached location
                if (!getCachedLocation()) {
                    setUserLocation(fallbackCenter);
                }
            }
        );

        // Chain loading: tracks -> beacons -> telemetry
        fetchRailways(setTracksData, setTrackDataLoaded)
            .then(() => fetchBeacons(setBeacons, setBeaconsLoaded))
            .then(() => fetchInitialTelemetryPins(setMapPins));
    }, []);

    useEffect(() => {
        if (!mapRef.current) return;
        const map = mapRef.current;

        // Remove existing panes if they exist
        const beaconPane = map.getPane('beaconPane');
        if (beaconPane && beaconPane.parentNode) beaconPane.parentNode.removeChild(beaconPane);

        const telemetryPane = map.getPane('telemetryPane');
        if (telemetryPane && telemetryPane.parentNode) telemetryPane.parentNode.removeChild(telemetryPane);

        map.createPane('beaconPane');
        map.getPane('beaconPane')!.style.zIndex = '400';

        map.createPane('telemetryPane');
        map.getPane('telemetryPane')!.style.zIndex = '500';
    }, [mapRef.current]);

    // Pan/zoom to userLocation when it changes and map is ready
    useEffect(() => {
        if (userLocation && mapRef.current) {
            // Set zoom to 11 when user location is found
            mapRef.current.setView(userLocation, 11);
            setMapZoom(11);
        }
    }, [userLocation]);

    function MapZoomListener() {
        useMapEvents({
            zoomend: (e) => setMapZoom(e.target.getZoom()),
        });
        return null;
    }

    // Load and clean up tracked pins on map load
    useEffect(() => {
        getTrackedMapPins();
    }, []);

    useEffect(() => {
        let wakeLock: any = null;

        async function requestWakeLock() {
            try {
                if ('wakeLock' in navigator) {
                    // @ts-ignore
                    wakeLock = await navigator.wakeLock.request('screen');
                    wakeLock.addEventListener('release', () => {
                        console.log('Screen Wake Lock released');
                    });
                    console.log('Screen Wake Lock acquired');
                }
            } catch (err) {
                console.error('Error acquiring wake lock:', err);
            }
        }

        requestWakeLock();

        // Re-acquire wake lock if the page becomes visible again
        const handleVisibilityChange = () => {
            if (wakeLock !== null && document.visibilityState === 'visible') {
                requestWakeLock();
            }
        };
        document.addEventListener('visibilitychange', handleVisibilityChange);

        return () => {
            document.removeEventListener('visibilitychange', handleVisibilityChange);
            if (wakeLock !== null) {
                wakeLock.release();
            }
        };
    }, []);

    // SOFT REFRESH ON FOCUS: Only refresh data, do NOT reload the page
    useEffect(() => {
        const handleVisibilityChange = () => {
            if (document.visibilityState === 'visible') {
                fetchRailways(setTracksData, setTrackDataLoaded)
                    .then(() => fetchBeacons(setBeacons, setBeaconsLoaded))
                    .then(() => fetchInitialTelemetryPins(setMapPins));
            }
        };
        document.addEventListener('visibilitychange', handleVisibilityChange);
        return () => {
            document.removeEventListener('visibilitychange', handleVisibilityChange);
        };
    }, []);

    return (
        <MapContainer
            center={fallbackCenter}
            zoom={mapZoom}
            style={{ height: '100%', width: '100%' }}
            scrollWheelZoom={true}
            ref={mapRef}
        >
            <MapZoomListener />
            <TileLayer
                url="https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png"
                attribution='&copy; <a href="https://carto.com/">CARTO</a>'
            />

            {/* Display railroad tracks using locally cached Overpass query of just WI and then generate GeoJSON file. */}
            {trackData && <GeoJSON 
                    data={trackData} 
                    style={(feature) => {
                        if (!feature || !feature.properties) return {};

                        const name = feature.properties?.name || '';

                        let color = 'gray';
                        let weight = 1;
                        if (name === 'CN Waukesha Subdivision') {
                            color = '#005aa9';
                            weight = 4;
                        }

                        return { color, weight };
                    }}
                />}

            {/* Beacon markers */}
            {trackDataLoaded && <BeaconMarkers pins={beaconPins} zoom={mapZoom} />}

            {/* Telemetry markers */}
            {trackDataLoaded && beaconsLoaded && <TelemetryMarkers
                pins={telemetryPins}
                zoom={mapZoom}
                maxPinAgeMinutes={MAX_PIN_AGE_MINUTES}
            />}

        </MapContainer>
    );
};

function updateMapPins(pins: MapPin[], newPin: MapPin): MapPin[] {
    const existingIndex = pins.findIndex(
        (mapPin) =>
            Array.isArray(mapPin.addresses) &&
            mapPin.addresses.some(addr1 =>
                Array.isArray(newPin.addresses) &&
                newPin.addresses.some(addr2 =>
                    addr1.addressID === addr2.addressID && addr1.source === addr2.source
                )
            )
    );

    if (existingIndex !== -1) {
        // Remove the existing map pin with the same address value
        pins.splice(existingIndex, 1);
    }

    // Add the new pin to the array
    return [...pins, newPin];
}

function updateBeacon(beacons: Beacon[], updatedBeacon: Beacon): Beacon[] {
    const existingIndex = beacons.findIndex(
        (beacon) => beacon.beaconID === updatedBeacon.beaconID && beacon.railroadID === updatedBeacon.railroadID
    );

    if (existingIndex !== -1) {
        // Remove the existing map pin with the same addressID
        beacons.splice(existingIndex, 1);
    }

    // Add the new pin to the array
    return [...beacons, updatedBeacon];
}

export default RailMap;
