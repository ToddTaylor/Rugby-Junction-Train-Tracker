import { AdminBeacon, CreateBeacon, UpdateBeacon } from '../types/AdminBeacon';
import { ApiResponse, fetchApi } from './client';

export async function getBeacons(): Promise<ApiResponse<AdminBeacon[]>> {
  return fetchApi<AdminBeacon[]>('/api/v1/Beacons');
}

export async function getBeaconById(id: number): Promise<ApiResponse<AdminBeacon>> {
  return fetchApi<AdminBeacon>(`/api/v1/Beacons/${id}`);
}

export async function createBeacon(beacon: CreateBeacon): Promise<ApiResponse<AdminBeacon>> {
  return fetchApi<AdminBeacon>('/api/v1/Beacons', {
    method: 'POST',
    body: JSON.stringify(beacon),
  });
}

export async function updateBeacon(id: number, beacon: UpdateBeacon): Promise<ApiResponse<AdminBeacon>> {
  return fetchApi<AdminBeacon>(`/api/v1/Beacons/${id}`, {
    method: 'PUT',
    body: JSON.stringify(beacon),
  });
}

export async function deleteBeacon(id: number): Promise<ApiResponse<null>> {
  return fetchApi<null>(`/api/v1/Beacons/${id}`, {
    method: 'DELETE',
  });
}
