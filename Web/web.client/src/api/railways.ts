import { openDB } from 'idb';
import { Beacon, MapPin } from '../types/types';

const DB_VERSION = 2;

export const fetchRailways = async (setTracksData: any, setTrackDataLoaded: any) => {
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
        setTracksData(cached);
        setTrackDataLoaded(true);
        return;
    }
    const response = await fetch('/data/overpass-wi-railways.geojson');
    if (!response.ok) throw new Error('Failed to fetch railways');
    const data = await response.json();
    await db.put(STORE_NAME, data, 'railways');
    setTracksData(data);
    setTrackDataLoaded(true);
};

export const fetchBeacons = async (setBeacons: any, setBeaconsLoaded: any) => {
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
    } catch (error) {
        console.error('Error fetching map pins:', error);
    }
};

export const fetchInitialTelemetryPins = async (setMapPins: any) => {
    try {
        const minutesOldFilter = 15;
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
