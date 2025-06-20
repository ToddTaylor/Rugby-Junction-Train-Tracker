export type Address = {
    source: string;
    addressID: number;
};

export type Beacon = {
    beaconID: string;
    railroadID: string;
    latitude: number;
    longitude: number;
    milepost: number;
    online: Boolean;
}

export type Direction = 'N' | 'NE' | 'E' | 'SE' | 'S' | 'SW' | 'W' | 'NW';

export type MapPin = {
    id: string;
    addresses: Address[]; 
    latitude: number;
    longitude: number;
    milepost: number;
    direction: string;
    moving: Boolean;
    railroad: string;
    subdivision: string;
    lastUpdate: string;
}

export type Telemetry = {
    id: string;
    beaconID: number;
    addressID: number;
    trainID: number;
    moving: boolean;
    source: string;
    createdAt: string;
    lastUpdate: string;
}