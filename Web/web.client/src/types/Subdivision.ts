export interface Subdivision {
  id: number;
  railroadID: number;
  railroad: string;
  dpuCapable: boolean;
  name: string;
  createdAt: string;
  lastUpdate: string;
}

export interface CreateSubdivision {
  railroadID: number;
  dpuCapable: boolean;
  name: string;
}

export interface UpdateSubdivision {
  id: number;
  railroadID: number;
  dpuCapable: boolean;
  name: string;
}
