import { Address } from './Address';

export type MapPin = {
    id: string;
    addresses: Address[];
    beaconName: string;
    latitude: number;
    longitude: number;
    milepost: number;
    direction: string;
    moving: Boolean;
    railroad: string;
    subdivision: string;
    lastUpdate: string;
};
