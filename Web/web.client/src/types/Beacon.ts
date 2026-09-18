export type Beacon = {
    beaconID: string;
    beaconName: string;
    railroadID: string;
    subdivisionID: string; // required subdivision ID for querying
    railroad?: string; // optional railroad name if provided by API
    subdivision?: string; // optional subdivision name if provided by API
    latitude: number;
    longitude: number;
    milepost: number;
    online: boolean; // primitive boolean for reliable equality checks & persistence
    telemetryStale?: boolean; // true when health endpoint is pinging but telemetry is stale past threshold
    offlineNote?: string | null; // note explaining why this beacon railroad is offline; cleared automatically once back online
    // Every subdivision passing through this physical beacon, each with its own milepost.
    // A junction such as Junction City - where one railroad crosses its own tracks - has
    // several entries here, all sharing the same coordinates. Populated when beacon railroad
    // rows are collapsed into a single marker; undefined means the single row itself.
    subdivisions?: BeaconSubdivision[];
};

/** One subdivision through a beacon, with the milepost that subdivision numbers it by. */
export type BeaconSubdivision = {
    subdivisionID: string;
    subdivision?: string;
    railroadID: string;
    railroad?: string;
    milepost: number;
};