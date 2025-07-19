// Utility for beacon dot sizing logic

export const BEACON_DOT_SIZE_BASE_PX = 12; // at zoom 11
export const BEACON_DOT_SIZE_MIN_PX = 12;
export const BEACON_DOT_SIZE_MAX_PX = 32;
export const BEACON_DOT_SIZE_PER_ZOOM_PX = 2;
export const BEACON_DOT_SIZE_BASE_ZOOM = 11; // Reference zoom level for base size

export function getBeaconDotSizePx(zoom: number): number {
    // At zoom BEACON_DOT_SIZE_BASE_ZOOM: base size. Each zoom above/below adds/subtracts px.
    return Math.max(
        BEACON_DOT_SIZE_MIN_PX,
        Math.min(
            BEACON_DOT_SIZE_MAX_PX,
            BEACON_DOT_SIZE_BASE_PX + (zoom - BEACON_DOT_SIZE_BASE_ZOOM) * BEACON_DOT_SIZE_PER_ZOOM_PX
        )
    );
}
