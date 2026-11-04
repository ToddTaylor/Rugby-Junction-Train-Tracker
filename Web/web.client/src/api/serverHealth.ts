import { ServerHealth, UpdateServerExpectations } from '../types/ServerHealth';
import { getCookie } from '../utils/cookies';
import { AuthSession } from '../types/Auth';

const API_URL = import.meta.env.VITE_API_URL;
const API_KEY = import.meta.env.VITE_API_KEY;
const SESSION_KEY = 'rjtt_auth_session';
const COOKIE_NAME = 'rjtt_auth';

function getAuthToken(): string | null {
  for (const raw of [localStorage.getItem(SESSION_KEY), getCookie(COOKIE_NAME), sessionStorage.getItem(SESSION_KEY)]) {
    if (!raw) continue;
    try {
      return (JSON.parse(raw) as AuthSession).token;
    } catch {
      // Ignore a malformed stored session and try the next location.
    }
  }

  return null;
}

interface ApiResponse<T> {
  data: T | null;
  errors: string[];
}

async function fetchApi<T>(endpoint: string, options?: RequestInit): Promise<ApiResponse<T>> {
  try {
    if (!API_KEY) {
      return { data: null, errors: ['Client API key is not configured (VITE_API_KEY).'] };
    }

    const token = getAuthToken();
    const headers: Record<string, string> = {
      'X-Api-Key': String(API_KEY),
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

    if (response.status === 204) {
      return { data: null, errors: [] };
    }

    const text = await response.text();
    const json = text ? JSON.parse(text) : null;

    if (!response.ok) {
      const fallback = response.status === 403 ? 'You do not have permission to do that.' : `Request failed (${response.status})`;
      return { data: null, errors: json?.errors?.length ? json.errors : [fallback] };
    }

    return { data: json?.data ?? null, errors: [] };
  } catch (error) {
    return { data: null, errors: [error instanceof Error ? error.message : 'Unknown error'] };
  }
}

export async function getServerHealth(): Promise<ApiResponse<ServerHealth[]>> {
  return fetchApi<ServerHealth[]>('/api/v1/ServerHealth');
}

export async function updateServerExpectations(beaconId: number, expectations: UpdateServerExpectations): Promise<ApiResponse<null>> {
  return fetchApi<null>(`/api/v1/ServerHealth/${beaconId}/expectations`, {
    method: 'PUT',
    body: JSON.stringify(expectations),
  });
}
