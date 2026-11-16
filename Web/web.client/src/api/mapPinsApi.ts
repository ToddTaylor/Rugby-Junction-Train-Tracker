import { fetchApi } from './client';
import type { MapPin } from '../types/MapPin';

/** Pins older than this many minutes are left out of the snapshot. */
const MAP_PINS_MAX_AGE_MINUTES = 15;

/**
 * Fetches the current map pins. The sign-in token is sent because the server returns more detail to Admins
 * and Custodians. Throws when the server cannot be reached or refuses, so a refresh that failed is not
 * mistaken for one that worked.
 */
export async function fetchMapPins(): Promise<MapPin[]> {
  const response = await fetchApi<MapPin[]>(`/api/v1/MapPins?minutes=${MAP_PINS_MAX_AGE_MINUTES}`);
  if (response.errors.length > 0 || !Array.isArray(response.data)) {
    throw new Error(`Failed to fetch map pins: ${response.errors.join(', ') || 'no data'}`);
  }

  return response.data;
}

// Admin: reset a map pin by deleting the map pin and all address IDs
export const deleteMapPinAddresses = async (id: number): Promise<void> => {
  const { getAuthToken } = await import('../services/auth');
  const token = await getAuthToken();
  const apiUrl = `${import.meta.env.VITE_API_URL}/api/v1/MapPins/${id}/addresses`;
  const response = await fetch(apiUrl, {
    method: 'DELETE',
    headers: {
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      'X-Api-Key': import.meta.env.VITE_API_KEY,
      'Content-Type': 'application/json'
    }
  });
  if (!response.ok) {
    throw new Error(`Failed to reset map pin ${id}: ${response.status}`);
  }
};
