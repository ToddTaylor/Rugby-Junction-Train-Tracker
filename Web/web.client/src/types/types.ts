export type Direction = 'N' | 'NE' | 'E' | 'SE' | 'S' | 'SW' | 'W' | 'NW';

export type Alert = {
    id: string;
    addressID: number;
    latitude: number;
    longitude: number;
    direction: string;
    moving: boolean;
    source: string;
    timestamp: string;
}