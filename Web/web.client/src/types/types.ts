export type Direction = 'N' | 'NE' | 'E' | 'SE' | 'S' | 'SW' | 'W' | 'NW';

export type MapPin = {
    id: string;
    addressID: number;
    latitude: number;
    longitude: number;
    direction: string;
    moving: Boolean;
    source: string;
    createdAt: string;
}