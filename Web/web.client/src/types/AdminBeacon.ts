export interface AdminBeacon {
  id: number;
  ownerID: number;
  name: string;
  createdAt: string;
  lastUpdate: string;
  beaconRailroads: BeaconRailroad[];
}

export interface BeaconRailroad {
  beaconID: number;
  beaconName: string;
  railroadID: number;
  railroadName: string;
  subdivisionID: number;
  subdivisionName: string;
  latitude: number;
  longitude: number;
  milepost: number;
  direction: string;
  online: boolean;
}

export interface CreateBeacon {
  ownerID: number;
  name: string;
}

export interface UpdateBeacon {
  id: number;
  ownerID: number;
  name: string;
}
