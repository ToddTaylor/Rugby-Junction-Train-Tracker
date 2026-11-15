import { Railroad, CreateRailroad, UpdateRailroad } from '../types/Railroad';
import { ApiResponse, fetchApi } from './client';

export async function getRailroads(): Promise<ApiResponse<Railroad[]>> {
  return fetchApi<Railroad[]>('/api/v1/Railroads');
}

export async function getRailroadById(id: number): Promise<ApiResponse<Railroad>> {
  return fetchApi<Railroad>(`/api/v1/Railroads/${id}`);
}

export async function createRailroad(railroad: CreateRailroad): Promise<ApiResponse<Railroad>> {
  return fetchApi<Railroad>('/api/v1/Railroads', {
    method: 'POST',
    body: JSON.stringify(railroad),
  });
}

export async function updateRailroad(id: number, railroad: UpdateRailroad): Promise<ApiResponse<Railroad>> {
  return fetchApi<Railroad>(`/api/v1/Railroads/${id}`, {
    method: 'PUT',
    body: JSON.stringify(railroad),
  });
}

export async function deleteRailroad(id: number): Promise<ApiResponse<void>> {
  return fetchApi<void>(`/api/v1/Railroads/${id}`, {
    method: 'DELETE',
  });
}
