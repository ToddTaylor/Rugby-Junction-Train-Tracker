import { useState, useEffect } from 'react';
import { MapPin } from '../types/MapPin';
import { getAuthToken } from '../services/auth';

export function useTelemetryPins() {
  const [mapPins, setMapPins] = useState<MapPin[]>([]);

  useEffect(() => {
    const fetchInitialTelemetryPins = async () => {
      try {
        const minutesOldFilter = 15;
        const apiUrl = import.meta.env.VITE_API_URL + "/api/v1/MapPins?minutes=" + minutesOldFilter;
        const token = await getAuthToken();
        const response = await fetch(apiUrl, {
          headers: {
            ...(token ? { Authorization: `Bearer ${token}` } : {}),
            'X-Api-Key': import.meta.env.VITE_API_KEY,
            'Content-Type': 'application/json'
          }
        });
        if (!response.ok) throw new Error('Failed to fetch map pins');
        const { data: pins } = await response.json();
        setMapPins(pins);
      } catch (error) {
        console.error('Error fetching map pins:', error);
      }
    };
    fetchInitialTelemetryPins();
  }, []);

  return { mapPins, setMapPins };
}
