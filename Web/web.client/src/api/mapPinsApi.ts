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
