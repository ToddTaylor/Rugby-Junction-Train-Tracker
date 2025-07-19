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
import BeaconMarkers from '../components/BeaconMarkers';
import TelemetryMarkers from '../components/TelemetryMarkers';
import { getTrackedMapPins } from '../services/trackedPins';
import { getCachedLocation, metersToLongitudeDegrees, pixelsToMeters } from '../utils/geo';
import { updateMapPins, updateBeacon } from '../utils/updateHelpers';
import { useRailways } from '../hooks/useRailways';
import { useBeacons } from '../hooks/useBeacons';
import { useTelemetryPins } from '../hooks/useTelemetryPins';

const fallbackCenter: LatLngTuple = [44.524570, -89.567290]; // Default if location fails

const RailMap: React.FC = () => {
    // Use cached location if available, else fallbackCenter
    const cachedLocation = getCachedLocation();
    const [userLocation, setUserLocation] = useState<LatLngTuple | null>(
        cachedLocation || fallbackCenter
    );
    // Set zoom to 11 if cached location exists, else 7
    const [mapZoom, setMapZoom] = useState<number>(cachedLocation ? 11 : 7);

    // Use custom hooks for data
    const { trackData, trackDataLoaded } = useRailways();
    const { beacons, beaconsLoaded, setBeacons } = useBeacons();
    const { mapPins, setMapPins } = useTelemetryPins();

    // SignalR updates
    useSignalR({
        MapPinUpdate: (mapPin: MapPin) => {
            setMapPins((prevPins: MapPin[]) => updateMapPins(prevPins, mapPin));
        },
        BeaconUpdate: (beacon: Beacon) => {
            setBeacons((prevBeacons: Beacon[]) => updateBeacon(prevBeacons, beacon));
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
        // Removed old fetch/set state functions, using hooks instead
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
                // fetchRailways(setTracksData, setTrackDataLoaded)
                //     .then(() => fetchBeacons(setBeacons, setBeaconsLoaded))
                //     .then(() => fetchInitialTelemetryPins(setMapPins));
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
            {trackDataLoaded && <BeaconMarkers pins={beacons} zoom={mapZoom} />}

            {/* Telemetry markers */}
            {trackDataLoaded && beaconsLoaded && <TelemetryMarkers
                pins={telemetryPins}
                zoom={mapZoom}
                maxPinAgeMinutes={MAX_PIN_AGE_MINUTES}
            />}

        </MapContainer>
    );
};

export default RailMap;
