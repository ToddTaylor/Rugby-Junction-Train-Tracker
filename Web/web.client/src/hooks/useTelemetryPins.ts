import { useCallback, useEffect, useState } from 'react';
import { MapPin } from '../types/MapPin';
import { fetchMapPins } from '../api/mapPinsApi';

export function useTelemetryPins() {
  const [mapPins, setMapPins] = useState<MapPin[]>([]);

  /**
   * Replaces the pins with the server's current snapshot. Rejects when the server cannot be reached,
   * leaving the pins as they were.
   */
  const refreshMapPins = useCallback(async () => {
    setMapPins(await fetchMapPins());
  }, []);

  useEffect(() => {
    refreshMapPins().catch(error => console.error('Error fetching map pins:', error));
  }, [refreshMapPins]);

  return { mapPins, setMapPins, refreshMapPins };
}
