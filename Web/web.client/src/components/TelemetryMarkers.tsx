import { MapPin } from '../types/MapPin';
import TelemetryMarker from './TelemetryMarker';
import type { TrackedPin } from '../services/trackedPins';
import { useMemo } from 'react';

// Add dynamic marker sizing based on zoom
function getMarkerSize(zoom: number): number {
    // Example: base size 28 at zoom 11, scales between 10 and 40
    return Math.max(10, Math.min(40, 28 + (zoom - 11) * 2));
}

interface TelemetryMarkersProps {
    pins: { [id: string]: MapPin };
    zoom: number;
    maxPinAgeMinutes: number;
    trackedPins: TrackedPin[];
}

const TelemetryMarkers: React.FC<TelemetryMarkersProps & { mapTheme: 'dark' | 'light' }> = ({
    pins: telemetryPins,
    zoom,
    maxPinAgeMinutes,
    trackedPins,
    mapTheme,
}) => {
    // Fix: Only render if pins is an object and has values
    if (!telemetryPins || typeof telemetryPins !== 'object') return null;

    // Memoize pinsArray to prevent unnecessary recalculations
    const pinsArray = useMemo(() => 
        Object.values(telemetryPins).filter(Boolean),
        [telemetryPins]
    );

    // Calculate size once for consistency
    const size = useMemo(() => getMarkerSize(zoom), [zoom]);

    // Group pins by beacon location and calculate horizontal offsets
    const pinsWithOffsets = useMemo(() => {
        const groups = new Map<string, MapPin[]>();
        
        // Group pins by beaconID
        pinsArray.forEach(pin => {
            if (pin.beaconID) {
                const key = String(pin.beaconID);
                if (!groups.has(key)) {
                    groups.set(key, []);
                }
                groups.get(key)!.push(pin);
            }
        });

        // Calculate offsets for pins in each group
        const result: Array<{ pin: MapPin; offset: number }> = [];
        
        // The actual visual circle is the full size, but we need more spacing than 7/8
        // to account for the fact that circles touching at 1/8 overlap looks too crowded
        // Use simple fixed pixel spacing that gives clear visual separation
        const pixelSpacing = size * 1.5; // 50% gap between circles
        
        groups.forEach(groupPins => {
            if (groupPins.length === 1) {
                result.push({ pin: groupPins[0], offset: 0 });
            } else {
                // Get average latitude for this group to calculate accurate spacing
                const avgLat = groupPins.reduce((sum, p) => sum + p.latitude, 0) / groupPins.length;
                
                // Convert pixel spacing to degrees longitude
                // Calculate how many meters per pixel at this zoom level and latitude
                const metersPerPixel = 156543.03392 * Math.cos(avgLat * Math.PI / 180) / Math.pow(2, zoom);
                
                // Convert pixels to meters
                const spacingMeters = pixelSpacing * metersPerPixel;
                
                // Convert meters to degrees longitude (longitude degrees vary with latitude)
                // At equator: 1 degree longitude ≈ 111,320m
                // At latitude: 1 degree longitude ≈ 111,320m * cos(latitude)
                const metersPerDegreeLongitude = 111320 * Math.cos(avgLat * Math.PI / 180);
                const spacingDegrees = spacingMeters / metersPerDegreeLongitude;
                
                const totalWidth = (groupPins.length - 1) * spacingDegrees;
                const startOffset = -totalWidth / 2;
                
                groupPins.forEach((pin, index) => {
                    const offset = startOffset + (index * spacingDegrees);
                    result.push({ pin, offset });
                });
            }
        });

        return result;
    }, [pinsArray, zoom, size]);

    return (
        <>
            {pinsWithOffsets.map(({ pin: telemetryPin, offset }) =>
                telemetryPin ? (
                    <TelemetryMarker
                        key={telemetryPin.id}
                        pin={telemetryPin}
                        size={size}
                        maxPinAgeMinutes={maxPinAgeMinutes}
                        trackedPins={trackedPins}
                        mapTheme={mapTheme}
                        longitudeOffset={offset}
                    />
                ) : null
            )}
        </>
    );
};

export default TelemetryMarkers;
