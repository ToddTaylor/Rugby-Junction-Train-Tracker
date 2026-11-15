import { AuthSession } from '../types/Auth';
import { fetchWithAuth } from '../utils/fetchWithAuth';

const API_URL = import.meta.env.VITE_API_URL;
const API_KEY = import.meta.env.VITE_API_KEY;
const STORAGE_KEY = 'rjtt_auth_session';

export interface ApiResponse<T> {
  data: T | null;
  errors: string[];
}

/**
 * The sign-in token of the current session, or null when nobody is signed in.
 * useAuth keeps the session in localStorage under the same key.
 */
export function getAuthToken(): string | null {
  const sessionData = localStorage.getItem(STORAGE_KEY);
  if (!sessionData) {
    return null;
  }

  try {
    return (JSON.parse(sessionData) as AuthSession).token ?? null;
  } catch {
    return null;
  }
}

/**
 * Calls the API with the client key and, when signed in, the sign-in token. The server asks for the token
 * and a role on every admin endpoint, so admin pages must go through here.
 * A 401 signs the user out (see fetchWithAuth); other failures come back in `errors`.
 */
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

    if (response.status === 204) {
      return { data: null as T, errors: [] };
    }

    const json = await response.json().catch(() => null);
    if (!response.ok) {
      return { data: null, errors: json?.errors?.length ? json.errors : ['Request failed'] };
    }

    return { data: json?.data ?? null, errors: [] };
  } catch (error) {
    return { data: null, errors: [error instanceof Error ? error.message : 'Unknown error'] };
  }
}
