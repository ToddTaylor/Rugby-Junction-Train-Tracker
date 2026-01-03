export interface SubdivisionTrackageRight {
  id: number;
  fromSubdivisionID: number;
  toSubdivisionID: number;
  fromSubdivisionName?: string;
  toSubdivisionName?: string;
  toRailroadName?: string;
}

export interface CreateSubdivisionTrackageRight {
  fromSubdivisionID: number;
  toSubdivisionID: number;
}
