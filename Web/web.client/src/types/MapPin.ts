import { Address } from './Address';

export type MapPin = {
    id: string;
    addresses: Address[];
    beaconID: string;
    beaconName: string;
    latitude: number;
    longitude: number;
    milepost: number;
    direction: string;
    moving: Boolean;
    isLocal: boolean;
    railroad: string;
    subdivision: string;
    lastUpdate: string;
};
