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
};