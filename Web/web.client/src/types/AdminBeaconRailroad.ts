export interface AdminBeaconRailroad {
  beaconID: number;
  beaconName: string;
  subdivisionID: number;
  subdivisionName: string;
  railroadID: number;
  railroadName: string;
  latitude: number;
  longitude: number;
  milepost: number;
  multipleTracks: boolean;
  online: boolean;
  direction: Direction;
  telemetryStaleHoursOverride?: number | null;
}

export type Direction = 'All' | 'NorthSouth' | 'EastWest' | 'NortheastSouthwest' | 'NorthwestSoutheast';

export interface CreateBeaconRailroad {
  beaconID: number;
  subdivisionID: number;
  latitude: number;
  longitude: number;
  milepost: number;
  multipleTracks: boolean;
  online: boolean;
  direction: Direction;
  telemetryStaleHoursOverride?: number | null;
}

export interface UpdateBeaconRailroad {
  beaconID: number;
  subdivisionID: number;
  latitude: number;
  longitude: number;
  milepost: number;
  multipleTracks: boolean;
  online: boolean;
  direction: Direction;
  telemetryStaleHoursOverride?: number | null;
}
