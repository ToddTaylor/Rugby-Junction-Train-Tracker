import { useEffect, useState } from 'react';

type UserLocation = {
    latitude: number;
    longitude: number;
    accuracy: number;
};

/**
 * Custom hook to manage user geolocation using watchPosition.
 * Returns the current location or null if not available/permission denied.
 */
export function useUserLocation(): UserLocation | null {
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

    return location;
}
