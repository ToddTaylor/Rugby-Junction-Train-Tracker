export interface AddressSnapshot {
    addressID: number;
    source: string;
    dpuTrainID?: number;
    createdAt: string;
    lastUpdate: string;
}

export interface MapPinHistory {
    id: number;
    originalMapPinID?: number;
    shareCode?: string;
    hasDpu?: boolean;
    addressSourceTypes?: string[];
    beaconID: number;
    beaconName?: string;
    subdivisionID: number;
    subdivision?: string;
    railroad?: string;
    latitude: number;
    longitude: number;
    milepost: number;
    direction?: string;
    moving?: boolean;
    isLocal: boolean;
    createdAt: string;
    lastUpdate: string;
    addresses?: AddressSnapshot[];
}
