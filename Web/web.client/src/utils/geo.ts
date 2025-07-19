// Utility functions for coordinate math and caching
import { LatLngTuple } from 'leaflet';

export function getCachedLocation(): LatLngTuple | null {
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

export function metersToLongitudeDegrees(meters: number, latitude: number): number {
    return meters / (111320 * Math.cos(latitude * Math.PI / 180));
}

export function pixelsToMeters(pixels: number, latitude: number, zoom: number): number {
    const metersPerPixel = (40075016.686 * Math.cos(latitude * Math.PI / 180)) / Math.pow(2, zoom + 8);
    return pixels * metersPerPixel;
}

// Add correct metersToPixels utility
export function metersToPixels(meters: number, latitude: number, zoom: number): number {
    const metersPerPixel = (40075016.686 * Math.cos(latitude * Math.PI / 180)) / Math.pow(2, zoom + 8);
    return meters / metersPerPixel;
}
