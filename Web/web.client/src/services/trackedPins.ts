import { getAuthToken } from './auth';

// Service for tracked map pins (uses API when available, falls back to localStorage)
export const TRACKED_KEY = 'trackedMapPins';
export const TRACK_COLORS = [
    '#FF3366', // Bright pink/red
    '#00FFFF', // Cyan
    '#00FF00', // Lime green
    '#FF00FF', // Magenta
    '#FFFF00', // Yellow
    '#FF6600', // Orange
    '#00FF99', // Spring green
    '#FF0099', // Hot pink
    '#66FF00', // Chartreuse
    '#0099FF', // Sky blue
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
                    return mapServerDtoToTrackedPin(serverDto, existing);
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

export async function addTrackedMapPin(id: string, beaconID?: string, subdivisionID?: string, beaconName?: string, symbol?: string, addresses?: Array<{ id: string; source: string }>) {
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

    // Add to local storage after API success
    const arr = getLocalTrackedPins();
    const usedColors = arr.map(item => item.color);
    const color = TRACK_COLORS.find(c => !usedColors.includes(c)) || 'orange';
    const now = Date.now();
    const expires = now + 12 * 60 * 60 * 1000; // 12 hours
    const newPin: TrackedPin = {
        id,
        mapPinId,
        expires,
        color,
        lastBeaconID: beaconID,
        lastSubdivisionID: subdivisionID,
        lastBeaconName: beaconName,
        symbol: symbol,
        addresses: addresses || []
    };
    arr.push(newPin);
    saveLocalTrackedPins(arr);
}

export async function removeTrackedMapPin(id: string) {
    const mapPinId = parseInt(id, 10);
    if (isNaN(mapPinId)) {
        console.error('Invalid map pin ID provided:', id);
        throw new Error('An unexpected error occurred. Please try again.');
    }

    // Delete from API first
    const headers = await getAuthHeader();
    const response = await fetch(`${API_BASE}/${mapPinId}`, {
        method: 'DELETE',
        headers
    });
    if (!response.ok) {
        const errorText = await response.text().catch(() => 'Unknown error');
        throw new Error(`Failed to remove tracked pin from API (${response.status} ${response.statusText}): ${errorText}`);
    }

    // Remove from local storage after API success
    const arr = getLocalTrackedPins().filter(item => item.id !== id);
    saveLocalTrackedPins(arr);
}

export function getTrackedColor(id: string): string | undefined {
    const arr = getLocalTrackedPins();
    return arr.find(item => item.id === id)?.color;
}

export async function updateTrackedPinLocation(id: string, beaconID: string, subdivisionID: string, beaconName: string | undefined, addresses?: Array<{ id: string; source: string }>) {
    const arr = getLocalTrackedPins();
    const tracked = arr.find(item => item.id === id);
    if (tracked) {
        tracked.lastBeaconID = beaconID;
        tracked.lastSubdivisionID = subdivisionID;
        if (beaconName !== undefined) {
            tracked.lastBeaconName = beaconName;
        }
        if (addresses !== undefined) {
            tracked.addresses = addresses.length > 0 ? [...addresses] : undefined;
        }
        saveLocalTrackedPins(arr);
    }

    // Persist to API (best-effort, background)
    const mapPinId = parseInt(id, 10);
    if (!Number.isFinite(mapPinId)) return;
    try {
        const headers = await getAuthHeader();
        await fetch(`${API_BASE}/${mapPinId}/location`, {
            method: 'PATCH',
            headers,
            body: JSON.stringify({
                beaconID: beaconID ? parseInt(beaconID, 10) : undefined,
                subdivisionID: subdivisionID ? parseInt(subdivisionID, 10) : undefined,
                beaconName
            })
        });
    } catch (err) {
        // Swallow errors; location will refresh on next add/update
        console.warn('Failed to sync tracked pin location', err);
    }
}

export async function updateTrackedPinSymbol(id: string, symbol: string) {
    const mapPinId = parseInt(id, 10);
    if (isNaN(mapPinId)) {
        console.error('Invalid map pin ID provided:', id);
        throw new Error('An unexpected error occurred. Please try again.');
    }

    // Update in API first
    const headers = await getAuthHeader();
    const response = await fetch(`${API_BASE}/${mapPinId}/symbol`, {
        method: 'PATCH',
        headers,
        body: JSON.stringify({ symbol })
    });
    if (!response.ok) {
        const errorText = await response.text().catch(() => 'Unknown error');
        throw new Error(`Failed to update tracked pin symbol in API (${response.status} ${response.statusText}): ${errorText}`);
    }

    // Update local storage after API success
    const arr = getLocalTrackedPins();
    const tracked = arr.find(item => item.id === id);
    if (tracked) {
        tracked.symbol = symbol;
        saveLocalTrackedPins(arr);
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
