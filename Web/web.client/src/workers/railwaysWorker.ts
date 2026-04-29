// Web Worker: fetch and parse railways GeoJSON off the main thread.
// Posts back { success: boolean, data?: GeoJSON.GeoJsonObject, error?: string }

const baseUrl = import.meta.env.BASE_URL || '/';
const normalizedBase = baseUrl.endsWith('/') ? baseUrl : `${baseUrl}/`;

const candidatePaths = [
  `${normalizedBase}data/usdot-wisconsin-no-fields.geojson`,
  '/data/usdot-wisconsin-no-fields.geojson'
];

async function fetchRailways(): Promise<GeoJSON.GeoJsonObject | null> {
  for (const path of candidatePaths) {
    try {
      const resp = await fetch(path);
      if (!resp.ok) {
        continue;
      }
      const text = await resp.text();
      // Parse manually to allow measuring time or adding future streaming logic.
      const json = JSON.parse(text);
      if (json && json.type === 'FeatureCollection') {
        return json as GeoJSON.GeoJsonObject;
      }
    } catch (e) {
      // Ignore and try next path.
    }
  }
  return null;
}

self.onmessage = async () => {
  try {
    const start = performance.now();
    const data = await fetchRailways();
    const end = performance.now();
    if (!data) {
      (self as unknown as Worker).postMessage({ success: false, error: 'Railways data not found via any candidate path.' });
      return;
    }
    (self as unknown as Worker).postMessage({ success: true, data, ms: end - start });
  } catch (err: any) {
    (self as unknown as Worker).postMessage({ success: false, error: err?.message || 'Unknown worker error.' });
  }
};
