// Service for tracked map pins (localStorage logic)
export const TRACKED_KEY = 'trackedMapPins';
export const TRACK_COLORS = [
    'red', 'blue', 'green', 'purple', 'orange', 'yellow', 'pink', 'teal', 'brown', 'gray',
];
export type TrackedPin = { id: string, expires: number, color: string };

export function getTrackedMapPins(): TrackedPin[] {
    const raw = localStorage.getItem(TRACKED_KEY);
    if (raw) {
        const arr = JSON.parse(raw) as TrackedPin[];
        const now = Date.now();
        const valid = arr.filter(item => item.expires > now);
        if (valid.length !== arr.length) {
            localStorage.setItem(TRACKED_KEY, JSON.stringify(valid));
        }
        return valid;
    }
    return [];
}

export function addTrackedMapPin(id: string) {
    const now = Date.now();
    const expires = now + 12 * 60 * 60 * 1000; // 12 hours
    const arr = getTrackedMapPins();
    const usedColors = arr.map(item => item.color);
    const color = TRACK_COLORS.find(c => !usedColors.includes(c)) || 'orange';
    arr.push({ id, expires, color });
    localStorage.setItem(TRACKED_KEY, JSON.stringify(arr));
}

export function removeTrackedMapPin(id: string) {
    const arr = getTrackedMapPins().filter(item => item.id !== id);
    localStorage.setItem(TRACKED_KEY, JSON.stringify(arr));
}

export function getTrackedColor(id: string): string | undefined {
    const arr = getTrackedMapPins();
    return arr.find(item => item.id === id)?.color;
}
