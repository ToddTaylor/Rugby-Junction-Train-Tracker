// --- New API helpers for tracked pin by unique ID ---
export async function updateTrackedPinSymbol(trackedPinId: string, symbol: string) {
    const headers = await getAuthHeader();
    const response = await fetch(`${API_BASE}/${trackedPinId}/symbol`, {
        method: 'PATCH',
        headers,
        body: JSON.stringify({ symbol })
    });
    if (!response.ok) {
        const errorText = await response.text().catch(() => 'Unknown error');
        throw new Error(`Failed to update tracked pin symbol by ID in API (${response.status} ${response.statusText}): ${errorText}`);
    }
    // Always refresh pins from backend after mutation
    return await refreshTrackedPinsFromApi();
}

export async function removeTrackedMapPin(trackedPinId: string) {
    const headers = await getAuthHeader();
    const response = await fetch(`${API_BASE}/${trackedPinId}`, {
        method: 'DELETE',
        headers
    });
    if (!response.ok) {
        const errorText = await response.text().catch(() => 'Unknown error');
        throw new Error(`Failed to remove tracked pin by ID from API (${response.status} ${response.statusText}): ${errorText}`);
    }
    // Always refresh pins from backend after mutation
    return await refreshTrackedPinsFromApi();
}

// Extend Window type to include mapPins
declare global {
    interface Window {
        mapPins?: any[];
    }
}
import { getAuthToken } from './auth';

// Service for tracked map pins (uses API when available, falls back to localStorage)
export const TRACKED_KEY = 'trackedMapPins';
export const TRACK_COLORS = [
    // Red
    '#FF3366', // Bright pink/red
    '#DC143C', // Crimson
    '#FF1493', // Deep pink
    '#FF6347', // Tomato
    // Orange
    '#FFD700', // Gold
    // Yellow
    '#FFFF00', // Yellow
    // Green
    '#00FF00', // Lime green
    '#66FF00', // Chartreuse
    '#7FFF00', // Chartreuse green
    '#228B22', // Forest green
    '#00FF99', // Spring green
    // Blue
    '#0099FF', // Sky blue
    '#1E90FF', // Dodger blue
    '#00CED1', // Dark turquoise
    // Indigo
    '#483D8B', // Dark slate blue
    '#8A2BE2', // Blue violet
    // Violet
    '#C71585', // Medium violet red
    '#FF00FF', // Magenta
    '#FF0099', // Hot pink
];

export type TrackedPin = {
    id: string;            // stringified MapPinId
    mapPinId?: number;     // numeric MapPinId (optional for legacy data)
    expires: number;       // Unix ms timestamp
    color: string;
    lastBeaconID?: string;
    lastSubdivisionID?: string;
    lastBeaconName?: string;
    symbol?: string;
    addresses?: Array<{ id: string; source: string }>;
};

const API_URL = import.meta.env.VITE_API_URL;
const API_KEY = import.meta.env.VITE_API_KEY;
const API_BASE = `${API_URL}/api/v1/UserTrackedPins`;

// ---- Local helpers (sync) ----
function getLocalTrackedPins(): TrackedPin[] {
    const raw = localStorage.getItem(TRACKED_KEY);
    if (raw) {
        try {
            const arr = JSON.parse(raw) as TrackedPin[];
            const now = Date.now();
            const valid = arr.filter(item => item.expires > now);
            if (valid.length !== arr.length) {
                localStorage.setItem(TRACKED_KEY, JSON.stringify(valid));
            }
            return valid;
        } catch (err) {
            console.error('Failed to parse tracked pins cache, clearing.', err);
            localStorage.removeItem(TRACKED_KEY);
        }
    }
    return [];
}

function saveLocalTrackedPins(pins: TrackedPin[]) {
    localStorage.setItem(TRACKED_KEY, JSON.stringify(pins));
}

// ---- Auth helper ----
async function getAuthHeader() {
    const token = await getAuthToken();
    if (!token) {
        throw new Error('Not authenticated');
    }
    return {
        Authorization: `Bearer ${token}`,
        'Content-Type': 'application/json',
        'X-Api-Key': API_KEY,
    };
}

// ---- Public API (sync for UI compatibility) ----
// Returns cached tracked pins synchronously (used throughout the UI)
export function getTrackedMapPins(): TrackedPin[] {
    return getLocalTrackedPins();
}

// Fetches from API, syncs cache, and returns latest pins
export async function refreshTrackedPinsFromApi(): Promise<TrackedPin[]> {
    try {
        const headers = await getAuthHeader();
        const response = await fetch(`${API_BASE}?t=${Date.now()}`, { headers });
        if (response.ok) {
            const text = await response.text();
            let data: any = null;
            try {
                data = JSON.parse(text);
            } catch (parseErr) {
                console.error('Tracked pins API did not return JSON. Falling back to cache.', parseErr);
                return getLocalTrackedPins();
            }

            const cachedPins = getLocalTrackedPins();

            if (data?.data) {
                // Try to get current mapPins from window (global RailMap state)
                let mapPins: any[] = [];
                if (typeof window !== 'undefined' && Array.isArray(window.mapPins)) {
                    mapPins = window.mapPins;
                } else {
                    // Fallback: fetch mapPins from API if not available in window
                    try {
                        const minutesOldFilter = 15;
                        const apiUrl = import.meta.env.VITE_API_URL + '/api/v1/MapPins?minutes=' + minutesOldFilter;
                        const response = await fetch(apiUrl, {
                            headers: {
                                'X-Api-Key': import.meta.env.VITE_API_KEY,
                                'Content-Type': 'application/json'
                            }
                        });
                        if (response.ok) {
                            const result = await response.json();
                            if (Array.isArray(result.data)) {
                                mapPins = result.data;
                            }
                        }
                    } catch (err) {
                        console.error('Failed to fetch mapPins for tracked pin address restoration:', err);
                    }
                }
                const pins: TrackedPin[] = data.data.map((pin: any) => {
                    const serverDto: TrackedPinServerDTO = {
                        mapPinId: pin.mapPinId,
                        beaconID: pin.beaconID,
                        subdivisionID: pin.subdivisionID,
                        beaconName: pin.beaconName,
                        symbol: pin.symbol,
                        color: pin.color,
                        expiresUtc: pin.expiresUtc
                    };
                    const existing = cachedPins.find(p => p.id === String(pin.mapPinId));
                    let trackedPin = mapServerDtoToTrackedPin(serverDto, existing);
                    // If addresses missing/empty, try to restore from mapPins
                    if (!trackedPin.addresses || trackedPin.addresses.length === 0) {
                        const mapPin = mapPins.find(mp => String(mp.id) === String(pin.mapPinId));
                        if (mapPin && Array.isArray(mapPin.addresses)) {
                            trackedPin.addresses = mapPin.addresses.map((addr: any) => ({ id: String(addr.addressID), source: addr.source }));
                        }
                    }
                    return trackedPin;
                });
                saveLocalTrackedPins(pins);
                return pins;
            }
        }
    } catch (error) {
        console.error('Error fetching tracked pins from API, using local cache:', error);
    }
    return getLocalTrackedPins();
}

export async function addTrackedMapPin(id: string, beaconID?: string, subdivisionID?: string, beaconName?: string, symbol?: string) {
    const mapPinId = parseInt(id, 10);
    if (isNaN(mapPinId)) {
        console.error('Invalid map pin ID provided:', id);
        throw new Error('An unexpected error occurred. Please try again.');
    }

    // Ensure beaconName is not empty
    if (!beaconName) beaconName = 'Unknown';

    // Persist to API first
    const headers = await getAuthHeader();
    const response = await fetch(API_BASE, {
        method: 'POST',
        headers,
        body: JSON.stringify({
            mapPinId,
            beaconID: beaconID ? parseInt(beaconID, 10) : undefined,
            subdivisionID: subdivisionID ? parseInt(subdivisionID, 10) : undefined,
            beaconName,
            symbol,
            color: TRACK_COLORS.find(c => !getLocalTrackedPins().map(item => item.color).includes(c)) || 'orange'
        })
    });
    if (!response.ok) {
        const errorText = await response.text().catch(() => 'Unknown error');
        throw new Error(`Failed to add tracked pin to API (${response.status} ${response.statusText}): ${errorText}`);
    }

    // After API success, fetch latest from server and update local cache
    const pins = await refreshTrackedPinsFromApi();
    window.dispatchEvent(new Event('storage'));
    return pins;
}


export function getTrackedColor(id: string): string | undefined {
    const arr = getLocalTrackedPins();
    return arr.find(item => item.id === id)?.color;
}

export async function updateTrackedPinLocation(id: string, beaconID: string, subdivisionID: string, beaconName: string | undefined) {
    // Always persist to API, then refresh from backend
    const mapPinId = parseInt(id, 10);
    if (!Number.isFinite(mapPinId)) return;
    try {
        const headers = await getAuthHeader();
        let response = await fetch(`${API_BASE}/${mapPinId}/location`, {
            method: 'PATCH',
            headers,
            body: JSON.stringify({
                beaconID: beaconID ? parseInt(beaconID, 10) : undefined,
                subdivisionID: subdivisionID ? parseInt(subdivisionID, 10) : undefined,
                beaconName
            })
        });
        if (!response.ok) {
            const errorText = await response.text().catch(() => 'Unknown error');
            throw new Error(`Failed to update tracked pin location in API (${response.status} ${response.statusText}): ${errorText}`);
        }
        return await refreshTrackedPinsFromApi();
    } catch (err) {
        // Swallow errors; location will refresh on next add/update
        console.warn('Failed to sync tracked pin location', err);
        return getLocalTrackedPins();
    }
}


export function getTrackedPinSymbol(id: string): string | undefined {
    const arr = getLocalTrackedPins();
    return arr.find(item => item.id === id)?.symbol;
}

export type TrackedPinServerDTO = {
    mapPinId: number;
    beaconID?: number;
    subdivisionID?: number;
    beaconName?: string;
    symbol?: string;
    color: string;
    expiresUtc: string;
};

function mapServerDtoToTrackedPin(dto: TrackedPinServerDTO, existing?: TrackedPin): TrackedPin {
    const expires = dto.expiresUtc ? new Date(dto.expiresUtc).getTime() : Date.now();
    return {
        id: String(dto.mapPinId),
        mapPinId: dto.mapPinId,
        expires,
        color: dto.color || existing?.color || 'orange',
        lastBeaconID: dto.beaconID ? String(dto.beaconID) : existing?.lastBeaconID,
        lastSubdivisionID: dto.subdivisionID ? String(dto.subdivisionID) : existing?.lastSubdivisionID,
        lastBeaconName: dto.beaconName ?? existing?.lastBeaconName,
        symbol: dto.symbol ?? existing?.symbol,
        addresses: existing?.addresses ? [...existing.addresses] : []
    };
}

// Apply server-pushed add/update without making additional API calls
export function applyTrackedPinAddedOrUpdatedFromServer(dto: TrackedPinServerDTO): TrackedPin[] {
    if (!dto || !dto.mapPinId) return getLocalTrackedPins();

    const currentPins = getLocalTrackedPins();
    const existing = currentPins.find(p => p.id === String(dto.mapPinId));
    if (!existing) return currentPins;

    const normalized = mapServerDtoToTrackedPin(dto, existing);
    const next = currentPins.filter(p => p.id !== normalized.id);
    next.push(normalized);
    saveLocalTrackedPins(next);
    return next;
}

// Apply server-pushed remove without making additional API calls
export function applyTrackedPinRemovedFromServer(mapPinId: number): TrackedPin[] {
    if (!mapPinId || Number.isNaN(mapPinId)) return getLocalTrackedPins();
    const filtered = getLocalTrackedPins().filter(p => p.mapPinId !== mapPinId && p.id !== String(mapPinId));
    saveLocalTrackedPins(filtered);
    return filtered;
}
