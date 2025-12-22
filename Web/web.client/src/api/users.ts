import { User, CreateUser, UpdateUser } from '../types/User';

const API_URL = import.meta.env.VITE_API_URL;
const API_KEY = import.meta.env.VITE_API_KEY;

interface ApiResponse<T> {
  data: T | null;
  errors: string[];
}

async function fetchApi<T>(endpoint: string, options?: RequestInit): Promise<ApiResponse<T>> {
  try {
    const response = await fetch(`${API_URL}${endpoint}`, {
      ...options,
      headers: {
        'X-Api-Key': API_KEY,
        'Content-Type': 'application/json',
        ...options?.headers,
      },
    });

    const json = await response.json();

    if (!response.ok) {
      return { data: null, errors: json.errors || ['Request failed'] };
    }

    return { data: json.data, errors: [] };
  } catch (error) {
    return { data: null, errors: [error instanceof Error ? error.message : 'Unknown error'] };
  }
}

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
  try {
    const response = await fetch(`${API_URL}/api/v1/Users/${id}`, {
      method: 'DELETE',
      headers: {
        'X-Api-Key': API_KEY,
      },
    });

    if (!response.ok) {
      const json = await response.json().catch(() => ({ errors: ['Delete failed'] }));
      return { success: false, errors: json.errors || ['Delete failed'] };
    }

    return { success: true, errors: [] };
  } catch (error) {
    return { success: false, errors: [error instanceof Error ? error.message : 'Unknown error'] };
  }
}
