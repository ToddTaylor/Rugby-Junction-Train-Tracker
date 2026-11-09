import type { ServerStatus } from './ServerHealth';

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
    // The beacon PC's confirmed Server Health status, shared by every row of the same beacon.
    // online is true only when this is 'Online'; Degraded and Offline beacons are drawn gray.
    serverStatus?: ServerStatus;
    serverStatusReasons?: string[]; // plain-language reasons for a Degraded or Offline status
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