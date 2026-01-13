export type Telemetry = {
    id: string;
    beaconID: number;
    beaconName: string;
    addressID: number;
    trainID: number;
    moving: boolean;
    source: string;
    createdAt: string;
    lastUpdate: string;
    discarded: boolean;
    discardReason?: string;
};
