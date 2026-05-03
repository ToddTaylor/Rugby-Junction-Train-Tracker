import { Address } from './Address';

export type MapPin = {
    id: string;
    shareCode?: string;
    addresses?: Address[];
    hasDpu?: boolean;
    addressSourceTypes?: string[];
    beaconID: string;
    beaconName: string;
    latitude: number;
    longitude: number;
    milepost: number;
    direction: string | null;
    moving: Boolean;
    isLocal: boolean;
    railroad: string;
    subdivision: string;
    subdivisionID: string;
    lastUpdate: string;
};
