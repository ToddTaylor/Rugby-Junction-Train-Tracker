import { User, CreateUser, UpdateUser } from '../types/User';
import { ApiResponse, fetchApi } from './client';

export { fetchApi };
export type { ApiResponse };

export async function getUsers(): Promise<ApiResponse<User[]>> {
  return fetchApi<User[]>('/api/v1/Users');
}

export async function getUserById(id: number): Promise<ApiResponse<User>> {
  return fetchApi<User>(`/api/v1/Users/${id}`);
}

export async function createUser(user: CreateUser): Promise<ApiResponse<User>> {
  return fetchApi<User>('/api/v1/Users', {
    method: 'POST',
    body: JSON.stringify(user),
  });
}

export async function updateUser(id: number, user: UpdateUser): Promise<ApiResponse<User>> {
  return fetchApi<User>(`/api/v1/Users/${id}`, {
    method: 'PUT',
    body: JSON.stringify(user),
  });
}

export async function deleteUser(id: number): Promise<{ success: boolean; errors: string[] }> {
  const response = await fetchApi<null>(`/api/v1/Users/${id}`, { method: 'DELETE' });
  return { success: response.errors.length === 0, errors: response.errors };
}
