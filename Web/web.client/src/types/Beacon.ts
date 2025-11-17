export type Beacon = {
    beaconID: string;
    beaconName: string;
    railroadID: string;
    latitude: number;
    longitude: number;
    milepost: number;
    online: boolean; // primitive boolean for reliable equality checks & persistence
};