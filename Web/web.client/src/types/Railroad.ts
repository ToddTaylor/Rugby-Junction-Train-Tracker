export interface Railroad {
  id: number;
  name: string;
  createdAt: string;
  lastUpdate: string;
}

export interface CreateRailroad {
  name: string;
}

export interface UpdateRailroad {
  id: number;
  name: string;
}
