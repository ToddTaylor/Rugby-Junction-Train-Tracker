import { Subdivision, CreateSubdivision, UpdateSubdivision } from '../types/Subdivision';
import { getCookie } from '../utils/cookies';
import { AuthSession } from '../types/Auth';

const API_URL = import.meta.env.VITE_API_URL;
const API_KEY = import.meta.env.VITE_API_KEY;
const SESSION_KEY = 'rjtt_auth_session';
const COOKIE_NAME = 'rjtt_auth';

function getAuthToken(): string | null {
  const cookieData = getCookie(COOKIE_NAME);
  if (cookieData) {
    try {
      const session = JSON.parse(cookieData) as AuthSession;
      return session.token;
    } catch { }
  }
  
  const sessionData = sessionStorage.getItem(SESSION_KEY);
  if (sessionData) {
    try {
      const session = JSON.parse(sessionData) as AuthSession;
      return session.token;
    } catch { }
  }
  
  return null;
}

interface ApiResponse<T> {
  data: T | null;
  errors: string[];
}

async function fetchApi<T>(endpoint: string, options?: RequestInit): Promise<ApiResponse<T>> {
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
    
    const { fetchWithAuth } = await import('../utils/fetchWithAuth');
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

export async function getSubdivisions(): Promise<ApiResponse<Subdivision[]>> {
  return fetchApi<Subdivision[]>('/api/v1/Subdivisions');
}

export async function getSubdivisionById(id: number): Promise<ApiResponse<Subdivision>> {
  return fetchApi<Subdivision>(`/api/v1/Subdivisions/${id}`);
}

export async function createSubdivision(subdivision: CreateSubdivision): Promise<ApiResponse<Subdivision>> {
  return fetchApi<Subdivision>('/api/v1/Subdivisions', {
    method: 'POST',
    body: JSON.stringify(subdivision),
  });
}

export async function updateSubdivision(id: number, subdivision: UpdateSubdivision): Promise<ApiResponse<Subdivision>> {
  return fetchApi<Subdivision>(`/api/v1/Subdivisions/${id}`, {
    method: 'PUT',
    body: JSON.stringify(subdivision),
  });
}

export async function deleteSubdivision(id: number): Promise<ApiResponse<void>> {
  return fetchApi<void>(`/api/v1/Subdivisions/${id}`, {
    method: 'DELETE',
  });
}
