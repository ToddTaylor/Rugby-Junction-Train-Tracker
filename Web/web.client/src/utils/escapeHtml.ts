const ENTITIES: Record<string, string> = {
  '&': '&amp;',
  '<': '&lt;',
  '>': '&gt;',
  '"': '&quot;',
  "'": '&#39;',
};

/**
 * Makes a value safe to place in an HTML string, as text or inside a quoted attribute.
 *
 * Leaflet popups and divIcons are built from HTML strings, so anything that comes from a user or from the
 * server (train symbols, beacon and railroad names, colors, provider data) must go through this first.
 * Otherwise a value such as a train symbol can carry markup that runs in every browser that shows it.
 * null and undefined become an empty string.
 */
export function escapeHtml(value: unknown): string {
  if (value === null || value === undefined) {
    return '';
  }

  return String(value).replace(/[&<>"']/g, char => ENTITIES[char]);
}
