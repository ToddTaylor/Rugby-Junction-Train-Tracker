import { fetchApi, ApiResponse } from './users';
import { AmtrakPollingConfiguration, AmtrakTrackedTrain, CreateAmtrakTrackedTrain, UpdateAmtrakPollingConfiguration } from '../types/Amtrak';

export async function getAmtrakTrackedTrains(): Promise<ApiResponse<AmtrakTrackedTrain[]>> {
  return fetchApi<AmtrakTrackedTrain[]>('/api/v1/Amtrak/trains');
}

export async function createAmtrakTrackedTrain(request: CreateAmtrakTrackedTrain): Promise<ApiResponse<AmtrakTrackedTrain>> {
  return fetchApi<AmtrakTrackedTrain>('/api/v1/Amtrak/trains', {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export async function deleteAmtrakTrackedTrain(id: number): Promise<ApiResponse<void>> {
  return fetchApi<void>(`/api/v1/Amtrak/trains/${id}`, {
    method: 'DELETE',
  });
}

export async function getAmtrakPollingConfiguration(): Promise<ApiResponse<AmtrakPollingConfiguration>> {
  return fetchApi<AmtrakPollingConfiguration>('/api/v1/Amtrak/polling');
}

export async function updateAmtrakPollingConfiguration(request: UpdateAmtrakPollingConfiguration): Promise<ApiResponse<AmtrakPollingConfiguration>> {
  return fetchApi<AmtrakPollingConfiguration>('/api/v1/Amtrak/polling', {
    method: 'PUT',
    body: JSON.stringify(request),
  });
}