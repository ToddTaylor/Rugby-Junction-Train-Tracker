// Railroad owner codes on the NTAD rail-lines features (RROWNER1/2/3).

export const UNKNOWN_OWNER = 'UNKNOWN';

// Railroads with train tracker coverage, highlighted until the user picks their own selection.
export const DEFAULT_SELECTED_OWNERS: readonly string[] = ['CN', 'CPKC', 'WSOR'];

const OWNER_FIELDS = ['RROWNER1', 'RROWNER2', 'RROWNER3'] as const;

const OWNER_NAMES: Record<string, string> = {
    BNSF: 'BNSF Railway',
    CN: 'Canadian National',
    CPKC: 'Canadian Pacific Kansas City',
    ELS: 'Escanaba & Lake Superior',
    FOXY: 'Fox Valley & Lake Superior',
    MCRY: 'Mid-Continent Railway',
    PVTX: 'Private',
    TR: 'Tomahawk Railway',
    UP: 'Union Pacific',
    USG: 'U.S. Government',
    WGNS: 'Wisconsin Great Northern',
    WN: 'Wisconsin Northern',
    WSOR: 'Wisconsin & Southern',
    XXXX: 'Unassigned',
    [UNKNOWN_OWNER]: 'Unknown',
};

export interface RailroadOwnerSummary {
    code: string;
    name: string;
    segmentCount: number;
}

// Every owner of a track segment. Segments with no owner recorded fall under UNKNOWN_OWNER.
export function getTrackOwners(properties: Record<string, unknown> | null | undefined): string[] {
    const owners = OWNER_FIELDS
        .map(field => properties?.[field])
        .filter((value): value is string => typeof value === 'string' && value.length > 0);
    return owners.length > 0 ? owners : [UNKNOWN_OWNER];
}

export function getOwnerName(code: string): string {
    return OWNER_NAMES[code] ?? code;
}

// A segment is highlighted when any of its owners is selected.
export function isTrackSelected(properties: Record<string, unknown> | null | undefined, selectedOwners: ReadonlySet<string>): boolean {
    return getTrackOwners(properties).some(owner => selectedOwners.has(owner));
}

// Owners found in the track data, with the number of segments each appears on, sorted by name.
export function summarizeOwners(trackData: GeoJSON.GeoJsonObject | null): RailroadOwnerSummary[] {
    const features = (trackData as GeoJSON.FeatureCollection | null)?.features;
    if (!Array.isArray(features)) return [];

    const counts = new Map<string, number>();
    for (const feature of features) {
        for (const owner of getTrackOwners(feature.properties)) {
            counts.set(owner, (counts.get(owner) ?? 0) + 1);
        }
    }

    return [...counts.entries()]
        .map(([code, segmentCount]) => ({ code, name: getOwnerName(code), segmentCount }))
        .sort((a, b) => a.name.localeCompare(b.name));
}
