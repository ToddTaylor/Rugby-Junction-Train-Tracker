import { ApiResponse, fetchApi } from './users';
import { PassengerMapPin } from '../types/PassengerMapPin';

export async function getPassengerMapPins(): Promise<ApiResponse<PassengerMapPin[]>> {
  return fetchApi<PassengerMapPin[]>('/api/v1/PassengerMapPins');
}