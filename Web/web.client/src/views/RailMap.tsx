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
import { Beacon } from '../types/Beacon';
import { MapPin } from '../types/MapPin';
import BeaconMarkers from '../components/BeaconMarkers';
import TelemetryMarkers from '../components/TelemetryMarkers';
import { getTrackedMapPins } from '../services/trackedPins';
import { metersToLongitudeDegrees, pixelsToMeters } from '../utils/geo';
import { updateMapPins, updateBeacon } from '../utils/updateHelpers';
import { useRailways } from '../hooks/useRailways';
import { useBeacons } from '../hooks/useBeacons';
import { useTelemetryPins } from '../hooks/useTelemetryPins';
import { useStaleRefresh } from '../hooks/useStaleRefresh';

const DARK_TILE_URL = "https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png";
const LIGHT_TILE_URL = "https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png";
const TILE_ATTRIBUTION = '&copy; <a href="https://carto.com/">CARTO</a>';

const fallbackCenter: LatLngTuple = [44.524570, -89.567290]; // Default if location fails

const RailMap: React.FC = () => {
    // Use cached location and zoom if available, else fallbackCenter and default zoom
    const savedMapState = JSON.parse(localStorage.getItem('mapState') || 'null');
    const [mapZoom, setMapZoom] = useState<number>(savedMapState?.zoom || 7);
    const [mapCenter, setMapCenter] = useState<LatLngTuple>(savedMapState?.center || fallbackCenter);

    // Use custom hooks for data
    const { trackData, trackDataLoaded } = useRailways();
    const { beacons, beaconsLoaded, setBeacons } = useBeacons();
    const { mapPins, setMapPins } = useTelemetryPins();

    // SignalR updates (retain reference to connection if needed for future reconnect logic)
    useSignalR({
        MapPinUpdate: (mapPin: MapPin) => {
            setMapPins((prevPins: MapPin[]) => updateMapPins(prevPins, mapPin));
        },
        BeaconUpdate: (beaconBatch: Beacon[]) => {
            setBeacons((prevBeacons: Beacon[]) => {
                let updated = prevBeacons;
                beaconBatch.forEach(b => {
                    updated = updateBeacon(updated, b);
                });
                return updated;
            });
        }
    });

    // Stale refresh on wake/tab visibility change (soft refresh first, fallback to hard reload handled inside hook)
    useStaleRefresh(setMapPins, setBeacons, () => { /* beacons loaded flag is maintained by hook's fetchBeacons */ }, {
        hiddenThresholdMsMobile: 30_000, // 30s for mobile
        hiddenThresholdMsDesktop: 120_000, // 2m desktop
        hardReloadFallbackMs: 10 * 60_000, // 10 minutes
        enableHardReload: true
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

    // Track last known update time and direction for each beacon, even after pin timeout
    const [beaconLastUpdateMap, setBeaconLastUpdateMap] = useState<{ [beaconID: string]: { lastUpdate: string, direction: string | null } }>({});

    // Seed beacon last update map from latest API on initial load
    useEffect(() => {
        let aborted = false;
        async function fetchLatest() {
            try {
                const apiUrl = import.meta.env.VITE_API_URL + '/api/v1/MapPins/latest';
                const response = await fetch(apiUrl, {
                    headers: {
                        'X-Api-Key': import.meta.env.VITE_API_KEY,
                        'Content-Type': 'application/json'
                    }
                });
                if (!response.ok) throw new Error('Failed to fetch latest map pins');
                const json = await response.json();
                const items: Array<{ beaconID: number | string; lastUpdate: string; direction: string | null }> = json?.data || [];
                if (aborted || !Array.isArray(items)) return;
                setBeaconLastUpdateMap(prev => {
                    const updated = { ...prev };
                    items.forEach(item => {
                        const key = String(item.beaconID);
                        const existing = updated[key];
                        if (!existing || new Date(item.lastUpdate) > new Date(existing.lastUpdate)) {
                            updated[key] = { lastUpdate: item.lastUpdate, direction: item.direction };
                        }
                    });
                    return updated;
                });
            } catch (e) {
                console.error('Error seeding latest beacon updates:', e);
            }
        }
        fetchLatest();
        return () => { aborted = true; };
    }, []);

    // Update last train time and direction for each beacon when telemetry pins change
    useEffect(() => {
        const newMap: { [beaconID: string]: { lastUpdate: string, direction: string | null } } = { ...beaconLastUpdateMap };
        mapPins.forEach(pin => {
            if (pin.beaconID && pin.lastUpdate) {
                const prev = newMap[pin.beaconID];
                if (!prev || new Date(pin.lastUpdate) > new Date(prev.lastUpdate)) {
                    newMap[pin.beaconID] = {
                        lastUpdate: pin.lastUpdate,
                        direction: pin.direction ?? null
                    };
                }
            }
        });
        setBeaconLastUpdateMap(newMap);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [mapPins]);

    // Build a map of pins by id for TelemetryMarkers (only non-expired pins)
    const telemetryPins: { [id: string]: MapPin } = {};
    offsetFilteredMarkers.forEach(pin => {
        telemetryPins[pin.id] = pin;
    });

    // Use ref to get the map instance
    const mapRef = useRef<LeafletMap | null>(null);

    useEffect(() => {
        if (!mapRef.current) return;
        const map = mapRef.current;

        // Remove existing panes if they exist
        const beaconPane = map.getPane('beaconPane');
        if (beaconPane && beaconPane.parentNode) beaconPane.parentNode.removeChild(beaconPane);

        const telemetryPane = map.getPane('telemetryPane');
        if (telemetryPane && telemetryPane.parentNode) telemetryPane.parentNode.removeChild(telemetryPane);

        map.createPane('beaconPane');
        const beaconPaneCreated = map.getPane('beaconPane');
        if (beaconPaneCreated) beaconPaneCreated.style.zIndex = '400';

        map.createPane('telemetryPane');
        const telemetryPaneCreated = map.getPane('telemetryPane');
        if (telemetryPaneCreated) telemetryPaneCreated.style.zIndex = '500';
    }, [mapRef.current]);

    // Save map center and zoom on move/zoom
    function MapZoomListener() {
        useMapEvents({
            zoomend: (e) => {
                setMapZoom(e.target.getZoom());
                setMapCenter([e.target.getCenter().lat, e.target.getCenter().lng]);
                localStorage.setItem('mapState', JSON.stringify({
                    center: [e.target.getCenter().lat, e.target.getCenter().lng],
                    zoom: e.target.getZoom()
                }));
            },
            moveend: (e) => {
                setMapCenter([e.target.getCenter().lat, e.target.getCenter().lng]);
                localStorage.setItem('mapState', JSON.stringify({
                    center: [e.target.getCenter().lat, e.target.getCenter().lng],
                    zoom: e.target.getZoom()
                }));
            }
        });
        return null;
    }

    // Load and clean up tracked pins on map load
    useEffect(() => {
        getTrackedMapPins();
    }, []);

    useEffect(() => {
        let wakeLock: any = null;

        async function requestWakeLockAndLocation() {
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

        requestWakeLockAndLocation();

        // Re-acquire wake lock if the page becomes visible again
        const handleVisibilityChange = () => {
            if (wakeLock !== null && document.visibilityState === 'visible') {
                requestWakeLockAndLocation();
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

    const [mapTheme, setMapTheme] = useState(() => localStorage.getItem('mapTheme') || 'dark');

    const handleToggleTheme = () => {
        setMapTheme(prev => {
            const next = prev === 'dark' ? 'light' : 'dark';
            localStorage.setItem('mapTheme', next);
            return next;
        });
    };

    const cacheBuster = import.meta.env.VITE_APP_VERSION
        ? `?v=${import.meta.env.VITE_APP_VERSION}`
        : `?t=${Date.now()}`;

    return (
        <>
            <div style={{ position: 'absolute', top: 10, right: 10, zIndex: 1000, display: 'flex', alignItems: 'center' }}>
                <span style={{ marginRight: 8 }}>
                    <img src={`/icons/sun.svg${cacheBuster}`} alt="Light mode" style={{ opacity: mapTheme === 'light' ? 1 : 0.4, width: 24, height: 24 }} />
                </span>
                <label style={{ display: 'inline-flex', alignItems: 'center', cursor: 'pointer' }}>
                    <input
                        type="checkbox"
                        checked={mapTheme === 'dark'}
                        onChange={handleToggleTheme}
                        style={{ display: 'none' }}
                    />
                    <span
                        style={{
                            width: 40,
                            height: 24,
                            background: mapTheme === 'dark' ? '#333' : '#bbb', // darker in light mode
                            borderRadius: 12,
                            position: 'relative',
                            transition: 'background 0.2s',
                            display: 'inline-block',
                        }}
                    >
                        <span
                            style={{
                                position: 'absolute',
                                left: mapTheme === 'dark' ? 20 : 2,
                                top: 2,
                                width: 20,
                                height: 20,
                                background: mapTheme === 'dark' ? '#fff' : '#f8f8f8',
                                borderRadius: '50%',
                                boxShadow: '0 1px 4px rgba(0,0,0,0.2)',
                                transition: 'left 0.2s, background 0.2s',
                            }}
                        />
                    </span>
                </label>
                <span style={{ marginLeft: 8 }}>
                    <img src={`/icons/moon.svg${cacheBuster}`} alt="Dark mode" style={{ opacity: mapTheme === 'dark' ? 1 : 0.4, width: 24, height: 24 }} />
                </span>
            </div>
            <MapContainer
                center={mapCenter}
                zoom={mapZoom}
                style={{ height: '100%', width: '100%' }}
                scrollWheelZoom={true}
                ref={mapRef}
            >
                <MapZoomListener />
                <TileLayer
                    url={mapTheme === 'dark' ? DARK_TILE_URL : LIGHT_TILE_URL}
                    attribution={TILE_ATTRIBUTION}
                />

                {/* Display railroad tracks using locally cached Overpass query of just WI and then generate GeoJSON file. */}
                {trackData && <GeoJSON 
                        data={trackData} 
                        style={(feature) => {
                            if (!feature || !feature.properties) return {};

                            const name = feature.properties?.name || '';

                            let color = 'gray';
                            let weight = 1;
                            const highlightSubs = [
                                'CN Waukesha Subdivision',
                                'Fox River Subdivision',
                                'Neenah Subdivision',
                                'Marinette Subdivision',
                                'Superior Subdivision',
                                'Valley Subdivision'
                                // Add more subdivision names here as needed
                            ];
                            if (highlightSubs.includes(name)) {
                                color = '#005aa9';
                                weight = 4;
                            }

                            return { color, weight };
                        }}
                    />}

                {/* Beacon markers */}
                {trackDataLoaded && <BeaconMarkers pins={beacons} zoom={mapZoom} mapTheme={mapTheme as 'dark' | 'light'} beaconLastUpdateMap={beaconLastUpdateMap} />}

                {/* Telemetry markers */}
                {trackDataLoaded && beaconsLoaded && <TelemetryMarkers
                    pins={telemetryPins}
                    zoom={mapZoom}
                    maxPinAgeMinutes={MAX_PIN_AGE_MINUTES}
                    mapTheme={mapTheme as 'dark' | 'light'}
                />}

            </MapContainer>
        </>
    );
};

export default RailMap;
