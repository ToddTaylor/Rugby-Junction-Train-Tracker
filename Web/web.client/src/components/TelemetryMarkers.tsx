import { MapPin } from '../types/types';
import TelemetryMarker from './TelemetryMarker';

// Add dynamic marker sizing based on zoom
function getMarkerSize(zoom: number): number {
    // Example: base size 28 at zoom 11, scales between 10 and 40
    return Math.max(10, Math.min(40, 28 + (zoom - 11) * 2));
}

interface TelemetryMarkersProps {
    pins: { [id: string]: MapPin };
    zoom: number;
    maxPinAgeMinutes: number;
}

function TelemetryMarkers({ pins: telemetryPins, zoom, maxPinAgeMinutes }: TelemetryMarkersProps) {
    const size = getMarkerSize(zoom);

    // Fix: Only render if pins is an object and has values
    if (!telemetryPins || typeof telemetryPins !== 'object') return null;

    const pinsArray = Object.values(telemetryPins).filter(Boolean);

    return (
        <>
            {pinsArray.map((telemetryPin) =>
                telemetryPin ? (
                    <TelemetryMarker
                        key={telemetryPin.id}
                        pin={telemetryPin}
                        size={size}
                        maxPinAgeMinutes={maxPinAgeMinutes}
                    />
                ) : null
            )}
        </>
    );
}

export default TelemetryMarkers;
