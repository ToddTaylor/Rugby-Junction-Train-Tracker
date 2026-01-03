import React, { useEffect, useState, useRef, useMemo } from 'react';
import {
    MapContainer,
    TileLayer,
    useMapEvents
} from 'react-leaflet';
import { LatLngTuple, Map as LeafletMap } from 'leaflet';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { useSignalR } from '../hooks/useSignalR';
import { Beacon } from '../types/Beacon';
import { MapPin } from '../types/MapPin';
import BeaconMarkers from '../components/BeaconMarkers';
import TelemetryMarkers from '../components/TelemetryMarkers';
import { BeaconHistoryModal } from '../components/BeaconHistoryModal';
import { getTrackedMapPins, updateTrackedPinLocation, refreshTrackedPinsFromApi, applyTrackedPinAddedOrUpdatedFromServer, applyTrackedPinRemovedFromServer } from '../services/trackedPins';
import { metersToLongitudeDegrees, pixelsToMeters } from '../utils/geo';
import { updateMapPins, updateBeacon } from '../utils/updateHelpers';
import { useRailways } from '../hooks/useRailways';
import { useBeacons } from '../hooks/useBeacons';
import { useTelemetryPins } from '../hooks/useTelemetryPins';
import { useStaleRefresh } from '../hooks/useStaleRefresh';
import { useAuth } from '../hooks/useAuth';

const DARK_TILE_URL = "https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png";
const LIGHT_TILE_URL = "https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png";
const TILE_ATTRIBUTION = '&copy; <a href="https://carto.com/">CARTO</a>';

const fallbackCenter: LatLngTuple = [44.524570, -89.567290]; // Default if location fails

const RailMap: React.FC = () => {
    // Use cached location and zoom if available, else fallbackCenter and default zoom
    const savedMapState = JSON.parse(localStorage.getItem('mapState') || 'null');
    const [mapZoom, setMapZoom] = useState<number>(savedMapState?.zoom || 7);
    const [mapCenter, setMapCenter] = useState<LatLngTuple>(savedMapState?.center || fallbackCenter);

    // Modal state for beacon history
    const [historyModalOpen, setHistoryModalOpen] = useState(false);
    const [selectedBeaconID, setSelectedBeaconID] = useState<string>('');
    const [selectedBeaconName, setSelectedBeaconName] = useState<string>('');
    const [selectedSubdivisionID, setSelectedSubdivisionID] = useState<string | undefined>(undefined);
    const [selectedRailroad, setSelectedRailroad] = useState<string | undefined>(undefined);
    const [selectedSubdivision, setSelectedSubdivision] = useState<string | undefined>(undefined);

    // Auth for admin button
    const { session } = useAuth();
    const isAdmin = session?.roles?.includes('Admin');

    // Use custom hooks for data
    const { trackData, trackDataLoaded, trackDataLoading } = useRailways();
    const { beacons, beaconsLoaded, setBeacons } = useBeacons();
    const { mapPins, setMapPins } = useTelemetryPins();

    // Track the current tracked pins in state to trigger re-renders when they change
    const [trackedPinsState, setTrackedPinsState] = useState(() => getTrackedMapPins());

    // On mount, pull latest tracked pins from API (syncs cache + state)
    useEffect(() => {
        refreshTrackedPinsFromApi().then(setTrackedPinsState).catch(() => {
            // fallback silently to cached state
        });
    }, []);

    // Periodically refresh tracked pins state (to handle expiration and updates)
    useEffect(() => {
        const interval = setInterval(() => {
            setTrackedPinsState(getTrackedMapPins());
        }, 5000); // Refresh cache view every 5 seconds
        return () => clearInterval(interval);
    }, []);

    // Sync tracked pin locations with current mapPins on load and when mapPins change
    useEffect(() => {
        const trackedPins = getTrackedMapPins();
        if (trackedPins.length === 0 || mapPins.length === 0) return;
        
        let updated = false;
        trackedPins.forEach(tracked => {
            const mapPin = mapPins.find(p => String(p.id) === String(tracked.id));
            if (mapPin && mapPin.beaconID && mapPin.subdivisionID && mapPin.beaconName) {
                const addresses = Array.isArray(mapPin.addresses)
                    ? mapPin.addresses.map(addr => ({ id: String(addr.addressID), source: addr.source }))
                    : undefined;
                const locationChanged = tracked.lastBeaconID !== mapPin.beaconID || tracked.lastSubdivisionID !== mapPin.subdivisionID;
                const addressesChanged = !!addresses && (
                    !tracked.addresses ||
                    tracked.addresses.length !== addresses.length ||
                    tracked.addresses.some((a, idx) => a.id !== addresses[idx].id || a.source !== addresses[idx].source)
                );
                if (locationChanged || addressesChanged) {
                    updateTrackedPinLocation(tracked.id, mapPin.beaconID, mapPin.subdivisionID, mapPin.beaconName, addresses);
                    updated = true;
                }
            }
        });
        
        if (updated) {
            setTrackedPinsState(getTrackedMapPins());
        }
    }, [mapPins]);

    // SignalR updates (retain reference to connection if needed for future reconnect logic)
    useSignalR({
        MapPinUpdate: (mapPin: MapPin) => {
            // Update tracked pin location if this is a tracked pin
            const trackedPins = getTrackedMapPins();
            const tracked = trackedPins.find(tp => String(tp.id) === String(mapPin.id));
            const addresses = Array.isArray(mapPin.addresses)
                ? mapPin.addresses.map(addr => ({ id: String(addr.addressID), source: addr.source }))
                : undefined;
            if (tracked && mapPin.beaconID && mapPin.subdivisionID && mapPin.beaconName) {
                const locationChanged = tracked.lastBeaconID !== mapPin.beaconID || tracked.lastSubdivisionID !== mapPin.subdivisionID;
                const addressesChanged = !!addresses && (
                    !tracked.addresses ||
                    tracked.addresses.length !== addresses.length ||
                    tracked.addresses.some((a, idx) => a.id !== addresses[idx].id || a.source !== addresses[idx].source)
                );
                if (locationChanged || addressesChanged) {
                    updateTrackedPinLocation(mapPin.id, mapPin.beaconID, mapPin.subdivisionID, mapPin.beaconName, addresses);
                    setTrackedPinsState(getTrackedMapPins());
                }
            }
            
            setMapPins((prevPins: MapPin[]) => updateMapPins(prevPins, mapPin));
            
            // Update beacon last update map
            if (mapPin.beaconID && mapPin.lastUpdate) {
                setBeaconLastUpdateMap(prev => {
                    const keyComposite = makeBeaconKey(mapPin.beaconID, mapPin.subdivisionID);
                    const existingComposite = prev[keyComposite];
                    if (!existingComposite || new Date(mapPin.lastUpdate) >= new Date(existingComposite.lastUpdate)) {
                        return {
                            ...prev,
                            [keyComposite]: { lastUpdate: mapPin.lastUpdate, direction: mapPin.direction ?? null }
                        };
                    }
                    return prev;
                });
            }
        },
        BeaconUpdate: (beaconBatch: Beacon[]) => {
            setBeacons((prevBeacons: Beacon[]) => {
                let updated = prevBeacons;
                beaconBatch.forEach(b => {
                    // Filter out beacons with invalid IDs (0 or null/undefined)
                    if (b.beaconID && Number(b.beaconID) !== 0 && b.railroadID && Number(b.railroadID) !== 0) {
                        updated = updateBeacon(updated, b);
                    }
                });
                return updated;
            });
        },
        TrackedPinAdded: (payload: any) => {
            const updated = applyTrackedPinAddedOrUpdatedFromServer(payload);
            setTrackedPinsState(updated);
        },
        TrackedPinUpdated: (payload: any) => {
            const updated = applyTrackedPinAddedOrUpdatedFromServer(payload);
            setTrackedPinsState(updated);
        },
        TrackedPinRemoved: (mapPinId: number) => {
            const updated = applyTrackedPinRemovedFromServer(mapPinId);
            setTrackedPinsState(updated);
        }
    });

    // Stale refresh on wake/tab visibility change (soft refresh first, fallback to hard reload handled inside hook)
    useStaleRefresh(setMapPins, setBeacons, () => { /* beacons loaded flag is maintained by hook's fetchBeacons */ }, {
        hiddenThresholdMsMobile: 30_000, // 30s for mobile
        hiddenThresholdMsDesktop: 120_000, // 2m desktop
        hardReloadFallbackMs: 10 * 60_000, // 10 minutes
        enableHardReload: true
    });

    // Marker icon size in pixels (constant)
    const MARKER_SIZE_PX = 20;

    // Memoize sorting, grouping, and offset calculations to avoid recomputation on unrelated renders
    const { groupedPins } = useMemo(() => {
        const sorted: MapPin[] = [...mapPins]
            .sort((a, b) => new Date(b.lastUpdate).getTime() - new Date(a.lastUpdate).getTime())
            .map(pin => ({ ...pin }));
        const grouped: { [key: string]: MapPin[] } = {};
        sorted.forEach(pin => {
            const key = `${pin.latitude},${pin.longitude}`;
            (grouped[key] ||= []).push(pin);
        });
        const offsets: MapPin[] = [];
        Object.values(grouped).forEach(group => {
            const n = group.length;
            if (n === 1) {
                offsets.push({ ...group[0], longitude: group[0].longitude });
            } else {
                group.forEach((pin, idx) => {
                    const offsetIndex = idx - (n - 1) / 2;
                    const offsetMeters = pixelsToMeters(MARKER_SIZE_PX, pin.latitude, mapZoom);
                    const offsetDeg = metersToLongitudeDegrees(offsetMeters * offsetIndex, pin.latitude);
                    offsets.push({ ...pin, longitude: pin.longitude + offsetDeg });
                });
            }
        });
        // offsetMarkers result not used directly; only groupedPins needed for downstream filtering
        return { groupedPins: grouped };
    }, [mapPins, mapZoom]);

    // Prune pins older than 10 minutes on a timer so they disappear even if no new data arrives
    // @ts-expect-error
    const [pruneTick, setPruneTick] = useState(0);
    useEffect(() => {
        const interval = setInterval(() => setPruneTick(tick => tick + 1), 30 * 1000);
        return () => clearInterval(interval);
    }, []);

    // Build a filtered list of pins that are not older than X minutes (.env variable),
    // but change pins where any address source == "EOT" to half the time (X/2 minutes)
    // For filtering (milliseconds)
    const MAX_PIN_AGE_MINUTES = Number(import.meta.env.VITE_MAX_PIN_AGE_MINUTES);
    const MAX_PIN_AGE_MS = MAX_PIN_AGE_MINUTES * 60 * 1000;
    const now = Date.now();
    const filteredPins: MapPin[] = useMemo(() => {
        const list: MapPin[] = [];
        Object.values(groupedPins).forEach(group => {
            group.forEach(pin => {
                const lastUpdate = new Date(pin.lastUpdate).getTime();
                const hasEOTOrDPU = Array.isArray(pin.addresses) && pin.addresses.some(addr => addr.source === 'EOT' || addr.source === 'DPU');
                const maxAge = hasEOTOrDPU ? MAX_PIN_AGE_MS / 2 : MAX_PIN_AGE_MS;
                if (now - lastUpdate <= maxAge) list.push(pin);
            });
        });
        return list;
    }, [groupedPins, now, MAX_PIN_AGE_MS]);

    // Group filtered pins by their lat/lng to handle overlapping markers
    const { offsetFilteredMarkers } = useMemo(() => {
        const grouped: { [key: string]: MapPin[] } = {};
        filteredPins.forEach(pin => {
            const key = `${pin.latitude},${pin.longitude}`;
            (grouped[key] ||= []).push(pin);
        });
        const offsets: MapPin[] = [];
        Object.values(grouped).forEach(group => {
            const n = group.length;
            if (n === 1) {
                offsets.push({ ...group[0], longitude: group[0].longitude });
            } else {
                group.forEach((pin, idx) => {
                    const offsetIndex = idx - (n - 1) / 2;
                    const offsetMeters = pixelsToMeters(MARKER_SIZE_PX, pin.latitude, mapZoom);
                    const offsetDeg = metersToLongitudeDegrees(offsetMeters * offsetIndex, pin.latitude);
                    offsets.push({ ...pin, longitude: pin.longitude + offsetDeg });
                });
            }
        });
        return { offsetFilteredMarkers: offsets };
    }, [filteredPins, mapZoom]);

    // Track last known update time and direction for each beacon, even after pin timeout.
    // Seed from localStorage so a full reload retains prior knowledge.
    // Clear old localStorage format (using railroad/subdivision names) to force refresh with new subdivisionID format
    const [beaconLastUpdateMap, setBeaconLastUpdateMap] = useState<{ [key: string]: { lastUpdate: string; direction: string | null } }>(() => {
        try {
            // Always clear old format on load to force refresh with new subdivisionID-based keys
            localStorage.removeItem('beaconLastUpdateMap');
        } catch { /* ignore */ }
        return {};
    });

    // Persist map to localStorage whenever it changes (lightweight JSON)
    useEffect(() => {
        try { localStorage.setItem('beaconLastUpdateMap', JSON.stringify(beaconLastUpdateMap)); } catch { /* ignore quota */ }
    }, [beaconLastUpdateMap]);

    // Helper to merge latest beacon timestamps from API
    const makeBeaconKey = (beaconID: string | number, subdivisionID?: string | number) => {
        return `${beaconID}${subdivisionID ? `|${subdivisionID}` : ''}`;
    };

    async function mergeLatestBeaconUpdates() {
        try {
            const apiUrl = import.meta.env.VITE_API_URL + '/api/v1/MapPins/latest';
            const response = await fetch(apiUrl, {
                headers: {
                    'X-Api-Key': import.meta.env.VITE_API_KEY,
                    'Content-Type': 'application/json'
                }
            });
            if (!response.ok) throw new Error(`Failed to fetch latest map pins: ${response.status}`);
            const json = await response.json();
            const items = (json?.data || []) as Array<any>;
            if (!Array.isArray(items)) {
                console.warn('Latest map pins response is not an array:', items);
                return;
            }
            
            setBeaconLastUpdateMap(prev => {
                const updated = { ...prev };
                items.forEach((item: any) => {
                    // Handle both PascalCase (from .NET) and camelCase
                    const beaconID = item.beaconID || item.BeaconID;
                    const subdivisionID = item.subdivisionID || item.SubdivisionID;
                    const lastUpdate = item.lastUpdate || item.LastUpdate;
                    const direction = item.direction || item.Direction;
                    
                    // Skip items with invalid IDs (missing, null, or 0)
                    if (!beaconID || beaconID === 0 || !subdivisionID || subdivisionID === 0 || !lastUpdate) {
                        console.warn('Skipping item with invalid beaconID, subdivisionID, or lastUpdate:', item);
                        return;
                    }
                    
                    const keyComposite = makeBeaconKey(beaconID, subdivisionID);
                    const existingComposite = updated[keyComposite];
                    if (!existingComposite || new Date(lastUpdate) >= new Date(existingComposite.lastUpdate)) {
                        updated[keyComposite] = { lastUpdate, direction: direction ?? null };
                    }
                });
                return updated;
            });
        } catch (e) {
            console.error('Error merging latest beacon updates:', e);
        }
    }

    // Initial seed
    useEffect(() => { mergeLatestBeaconUpdates(); }, []);

    // Refresh latest timestamps when page becomes visible (soft refresh scenario)
    useEffect(() => {
        function onVisibility() {
            if (document.visibilityState === 'visible') {
                mergeLatestBeaconUpdates();
            }
        }
        document.addEventListener('visibilitychange', onVisibility);
        return () => document.removeEventListener('visibilitychange', onVisibility);
    }, []);

    // Update last train time and direction for each beacon when telemetry pins change
    useEffect(() => {
        if (!mapPins.length) return;
        
        setBeaconLastUpdateMap(prev => {
            const updated = { ...prev };
            mapPins.forEach(pin => {
                if (pin.beaconID && pin.lastUpdate) {
                    const keyComposite = makeBeaconKey(pin.beaconID, pin.subdivisionID);
                    const prevEntryComposite = updated[keyComposite];
                    if (!prevEntryComposite || new Date(pin.lastUpdate) >= new Date(prevEntryComposite.lastUpdate)) {
                        updated[keyComposite] = {
                            lastUpdate: pin.lastUpdate,
                            direction: pin.direction ?? null
                        };
                    }
                }
            });
            return updated;
        });
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [mapPins]);

    // Build a map of pins by id for TelemetryMarkers (only non-expired pins)
    const telemetryPins: { [id: string]: MapPin } = {};
    offsetFilteredMarkers.forEach(pin => {
        telemetryPins[pin.id] = pin;
    });

    // Use ref to get the map instance
    const mapRef = useRef<LeafletMap | null>(null);
    const railLayerRef = useRef<L.GeoJSON | null>(null);

    // Imperative, single-instance GeoJSON layer creation & incremental population to avoid recursive re-render stack overflow
    useEffect(() => {
        if (!mapRef.current) return;
        if (trackDataLoading) return; // wait until loading completes
        if (!trackData) return; // nothing to render yet

        const map = mapRef.current;
        if (!railLayerRef.current) {
            railLayerRef.current = L.geoJSON(undefined, {
                style: () => ({ color: '#005aa9', weight: 4 })
            }).addTo(map);
        }
        const layer = railLayerRef.current!;
        layer.clearLayers();

        const features: any[] | undefined = (trackData as any)?.features;
        if (Array.isArray(features)) {
            const featuresArr = features as any[];
            let i = 0;
            const chunkSize = 500; // tune chunk size if needed
            function addNext() {
                const slice = { ...trackData, features: featuresArr.slice(i, i + chunkSize) };
                layer.addData(slice as any);
                i += chunkSize;
                if (i < featuresArr.length) {
                    setTimeout(addNext, 0); // yield to event loop to keep UI responsive
                }
            }
            addNext();
        } else {
            layer.addData(trackData as any);
        }
    }, [trackData, trackDataLoading]);

    // Cleanup on unmount
    useEffect(() => {
        return () => {
            if (railLayerRef.current && mapRef.current) {
                mapRef.current.removeLayer(railLayerRef.current);
                railLayerRef.current = null;
            }
        };
    }, []);

    useEffect(() => {
        if (!mapRef.current) return;
        const map = mapRef.current;

        // Remove existing panes if they exist
        const beaconPane = map.getPane('beaconPane');
        if (beaconPane && beaconPane.parentNode) beaconPane.parentNode.removeChild(beaconPane);

        const beaconLabelPane = map.getPane('beaconLabelPane');
        if (beaconLabelPane && beaconLabelPane.parentNode) beaconLabelPane.parentNode.removeChild(beaconLabelPane);

        const telemetryPane = map.getPane('telemetryPane');
        if (telemetryPane && telemetryPane.parentNode) telemetryPane.parentNode.removeChild(telemetryPane);

        map.createPane('beaconPane');
        const beaconPaneCreated = map.getPane('beaconPane');
        if (beaconPaneCreated) beaconPaneCreated.style.zIndex = '400';

        map.createPane('beaconLabelPane');
        const beaconLabelPaneCreated = map.getPane('beaconLabelPane');
        if (beaconLabelPaneCreated) beaconLabelPaneCreated.style.zIndex = '450';

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
            <div style={{ position: 'absolute', top: 10, right: 10, zIndex: 1000, display: 'flex', alignItems: 'center', gap: 12 }}>
                {isAdmin && (
                    <a href="/admin" style={{
                        background: '#007bff',
                        color: '#fff',
                        padding: '8px 16px',
                        borderRadius: '4px',
                        textDecoration: 'none',
                        fontSize: '14px',
                        fontWeight: 500,
                        transition: 'background 0.2s',
                    }}>
                        Admin
                    </a>
                )}
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
                style={{ height: historyModalOpen ? '60vh' : '100%', width: '100%', transition: 'height 0.3s' }}
                scrollWheelZoom={true}
                ref={mapRef}
            >
                <MapZoomListener />
                <TileLayer
                    url={mapTheme === 'dark' ? DARK_TILE_URL : LIGHT_TILE_URL}
                    attribution={TILE_ATTRIBUTION}
                />

                {/* Railroad tracks added imperatively via Leaflet GeoJSON layer */}

                {trackDataLoading && (
                    <div style={{
                        position: 'absolute',
                        top: '50%',
                        left: '50%',
                        transform: 'translate(-50%, -50%)',
                        background: 'rgba(0,0,0,0.6)',
                        color: '#fff',
                        padding: '8px 14px',
                        borderRadius: 6,
                        zIndex: 900,
                        fontSize: 14,
                        letterSpacing: '0.5px'
                    }}>
                        Loading railways…
                    </div>
                )}

                {/* Beacon markers */}
                {trackDataLoaded && <BeaconMarkers 
                    pins={beacons} 
                    zoom={mapZoom} 
                    mapTheme={mapTheme as 'dark' | 'light'} 
                    beaconLastUpdateMap={beaconLastUpdateMap}
                    onBeaconClick={(beaconID, beaconName, subdivisionID, railroad, subdivision) => {
                        setSelectedBeaconID(beaconID);
                        setSelectedBeaconName(beaconName);
                        setSelectedSubdivisionID(subdivisionID);
                        setSelectedRailroad(railroad);
                        setSelectedSubdivision(subdivision);
                        setHistoryModalOpen(true);
                    }}
                    trackedPins={trackedPinsState}
                    mapPins={mapPins}
                />}

                {/* Telemetry markers */}
                {trackDataLoaded && beaconsLoaded && <TelemetryMarkers
                    pins={telemetryPins}
                    zoom={mapZoom}
                    maxPinAgeMinutes={MAX_PIN_AGE_MINUTES}
                    trackedPins={trackedPinsState}
                    mapTheme={mapTheme as 'dark' | 'light'}
                />}

            </MapContainer>

            {/* Beacon History Modal */}
            <BeaconHistoryModal
                open={historyModalOpen}
                onClose={() => setHistoryModalOpen(false)}
                beaconID={selectedBeaconID}
                beaconName={selectedBeaconName}
                subdivisionID={selectedSubdivisionID}
                railroad={selectedRailroad}
                subdivision={selectedSubdivision}
                theme={mapTheme as 'dark' | 'light'}
                lastUpdate={beaconLastUpdateMap?.[makeBeaconKey(selectedBeaconID, selectedSubdivisionID)]?.lastUpdate}
                mapPins={mapPins}
            />
        </>
    );
};

export default RailMap;
