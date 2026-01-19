import { User, CreateUser, UpdateUser } from '../types/User';
import { fetchWithAuth } from '../utils/fetchWithAuth';
import { AuthSession } from '../types/Auth';

const API_URL = import.meta.env.VITE_API_URL;
const API_KEY = import.meta.env.VITE_API_KEY;
const STORAGE_KEY = 'rjtt_auth_session';

function getAuthToken(): string | null {
  const sessionData = localStorage.getItem(STORAGE_KEY);
  if (sessionData) {
    try {
      const session = JSON.parse(sessionData) as AuthSession;
      return session.token;
    } catch { }
  }
  return null;
}

export interface ApiResponse<T> {
  data: T | null;
  errors: string[];
}

export async function fetchApi<T>(endpoint: string, options?: RequestInit): Promise<ApiResponse<T>> {
  try {
    const token = getAuthToken();
    const headers: Record<string, string> = {
      'X-Api-Key': API_KEY,
      'Content-Type': 'application/json',
      ...options?.headers as Record<string, string>,
    };
    if (token) {
      headers['X-Auth-Token'] = token;
    }
    const response = await fetchWithAuth(`${API_URL}${endpoint}`, {
      ...options,
      headers,
    });
    // Handle 204 No Content responses
    if (response.status === 204) {
      return { data: null as T, errors: [] };
    }
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
