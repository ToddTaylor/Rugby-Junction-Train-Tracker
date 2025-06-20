const TRACKED_KEY = 'trackedMapPins';
const TRACK_COLORS = [
    'orange',      // 1st tracked
    '#1976d2',     // 2nd tracked (blue)
    '#43a047',     // 3rd tracked (green)
    '#d32f2f',     // 4th tracked (red)
    '#fbc02d',     // 5th tracked (yellow)
    '#8e24aa',     // 6th tracked (purple)
    '#00838f',     // 7th tracked (teal)
    '#f57c00',     // 8th tracked (deep orange)
    '#5d4037',     // 9th tracked (brown)
    '#c2185b',     // 10th tracked (pink)
];

export type TrackedPin = { id: string, expires: number, color: string };

export function getTrackedMapPins(): TrackedPin[] {
    const raw = localStorage.getItem(TRACKED_KEY);
    if (!raw) return [];
    try {
        const arr = JSON.parse(raw) as TrackedPin[];
        const now = Date.now();
        // Remove expired
        const valid = arr.filter(item => item.expires > now);
        if (valid.length !== arr.length) {
            localStorage.setItem(TRACKED_KEY, JSON.stringify(valid));
        }
        return valid;
    } catch {
        localStorage.removeItem(TRACKED_KEY);
        return [];
    }
}

export function addTrackedMapPin(id: string) {
    const now = Date.now();
    const expires = now + 12 * 60 * 60 * 1000; // 12 hours
    let arr: TrackedPin[] = [];
    try {
        arr = JSON.parse(localStorage.getItem(TRACKED_KEY) || '[]');
    } catch {}
    // Remove expired and duplicates
    arr = arr.filter(item => item.expires > now && item.id !== id);

    // Assign next available color
    const usedColors = arr.map(item => item.color);
    const color = TRACK_COLORS.find(c => !usedColors.includes(c)) || 'orange';

    arr.push({ id, expires, color });
    localStorage.setItem(TRACKED_KEY, JSON.stringify(arr));
}

export function removeTrackedMapPin(id: string) {
    const now = Date.now();
    let arr: TrackedPin[] = [];
    try {
        arr = JSON.parse(localStorage.getItem(TRACKED_KEY) || '[]');
    } catch {}
    arr = arr.filter(item => item.expires > now && item.id !== id);
    localStorage.setItem(TRACKED_KEY, JSON.stringify(arr));
}

export function getTrackedColor(id: string): string | undefined {
    const arr = getTrackedMapPins();
    return arr.find(item => item.id === id)?.color;
}