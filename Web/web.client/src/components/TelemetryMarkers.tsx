import { MapPin } from '../types/types';
import TelemetryMarker from './TelemetryMarker';

// Add dynamic marker sizing based on zoom
function getMarkerSize(zoom: number): number {
    // Base size at zoom 11 is 20px, scale up/down with zoom
    // Clamp between 10px and 40px for usability
    return Math.max(10, Math.min(40, 20 + (zoom - 11) * 2));
}

interface TelemetryMarkersProps {
    pins: { [id: string]: MapPin };
    zoom: number;
}

function TelemetryMarkers({ pins: telemetryPins, zoom }: TelemetryMarkersProps) {
    const size = getMarkerSize(zoom);

    return (
        <>
            {Object.values(telemetryPins).map((telemetryPin) => (
                <TelemetryMarker key={telemetryPin.id} pin={telemetryPin} size={size} />
            ))}
        </>
    );
}

export default TelemetryMarkers;
