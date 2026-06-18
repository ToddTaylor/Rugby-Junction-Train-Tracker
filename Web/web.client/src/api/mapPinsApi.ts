// Fetch initial telemetry (map pins) snapshot
export const fetchInitialTelemetryPins = async (setMapPins: any) => {
  try {
    const minutesOldFilter = 15;
    const apiUrl = import.meta.env.VITE_API_URL + '/api/v1/MapPins?minutes=' + minutesOldFilter;
    const response = await fetch(apiUrl, {
      headers: {
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
