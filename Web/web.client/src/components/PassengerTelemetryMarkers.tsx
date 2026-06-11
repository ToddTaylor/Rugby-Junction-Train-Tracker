import PassengerTelemetryMarker from './PassengerTelemetryMarker';
import { PassengerMapPin } from '../types/PassengerMapPin';

type Props = {
  pins: PassengerMapPin[];
  zoom: number;
  mapTheme: 'dark' | 'light';
};

function getMarkerSize(zoom: number): number {
  return Math.max(10, Math.min(40, 28 + (zoom - 11) * 2));
}

const PassengerTelemetryMarkers: React.FC<Props> = ({ pins, zoom, mapTheme }) => {
  const size = getMarkerSize(zoom);
  const visiblePins = pins.filter(pin => !pin.isStale);

  return (
    <>
      {visiblePins.map((pin) => (
        <PassengerTelemetryMarker
          key={`passenger-${pin.trainId}`}
          pin={pin}
          size={size}
          mapTheme={mapTheme}
        />
      ))}
    </>
  );
};

export default PassengerTelemetryMarkers;