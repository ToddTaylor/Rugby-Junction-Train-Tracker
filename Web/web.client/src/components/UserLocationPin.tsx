import { useEffect, useMemo, useState } from 'react';
import { Circle, CircleMarker, Tooltip } from 'react-leaflet';

type UserLocationPinProps = {
    mapTheme: 'dark' | 'light';
};

type UserLocation = {
    latitude: number;
    longitude: number;
    accuracy: number;
};

const USER_LOCATION_PANE = 'userLocationPane';

const UserLocationPin: React.FC<UserLocationPinProps> = ({ mapTheme }) => {
    const [location, setLocation] = useState<UserLocation | null>(null);

    useEffect(() => {
        if (!('geolocation' in navigator)) {
            return;
        }

        const watchId = navigator.geolocation.watchPosition(
            (position) => {
                const { latitude, longitude, accuracy } = position.coords;
                setLocation({ latitude, longitude, accuracy });
            },
            (error) => {
                if (error.code !== error.PERMISSION_DENIED) {
                    console.warn('User location update failed:', error.message);
                }
            },
            {
                enableHighAccuracy: true,
                maximumAge: 5000,
                timeout: 20000
            }
        );

        return () => {
            navigator.geolocation.clearWatch(watchId);
        };
    }, []);

    const markerColor = useMemo(() => {
        return mapTheme === 'dark' ? '#7dd3fc' : '#0b5cad';
    }, [mapTheme]);

    if (!location) {
        return null;
    }

    const center: [number, number] = [location.latitude, location.longitude];
    const accuracyRadius = Math.min(Math.max(location.accuracy, 8), 350);

    return (
        <>
            <Circle
                center={center}
                radius={accuracyRadius}
                pane={USER_LOCATION_PANE}
                pathOptions={{
                    color: markerColor,
                    fillColor: markerColor,
                    fillOpacity: mapTheme === 'dark' ? 0.12 : 0.08,
                    weight: 1
                }}
                interactive={false}
            />
            <CircleMarker
                center={center}
                radius={6}
                pane={USER_LOCATION_PANE}
                pathOptions={{
                    color: '#ffffff',
                    fillColor: markerColor,
                    fillOpacity: 1,
                    weight: 2
                }}
            >
                <Tooltip direction="top" offset={[0, -8]} opacity={0.95} permanent={false}>
                    Your location
                </Tooltip>
            </CircleMarker>
        </>
    );
};

export default UserLocationPin;
