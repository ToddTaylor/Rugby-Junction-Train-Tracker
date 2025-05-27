export type Direction = 'N' | 'NE' | 'E' | 'SE' | 'S' | 'SW' | 'W' | 'NW';

export type Beacon = {
    beaconID: string;
    railroadID: string;
    latitude: number;
    longitude: number;
    milepost: number;
    online: Boolean;
}

export type MapPin = {
    id: string;
    addressID: number;
    latitude: number;
    longitude: number;
    milepost: number;
    direction: string;
    moving: Boolean;
    railroad: string;
    source: string;
    subdivision: string;
    lastUpdate: string;
}