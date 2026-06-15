import { SubdivisionTrackageRight } from '../types/SubdivisionTrackageRight';
import { getCookie } from '../utils/cookies';
import { AuthSession } from '../types/Auth';

const API_URL = import.meta.env.VITE_API_URL;
const API_KEY = import.meta.env.VITE_API_KEY;
const SESSION_KEY = 'rjtt_auth_session';
const COOKIE_NAME = 'rjtt_auth';

function getAuthToken(): string | null {
    const localData = localStorage.getItem(SESSION_KEY);
    if (localData) {
        try {
            const session = JSON.parse(localData) as AuthSession;
            return session.token;
        } catch { }
    }

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

export async function getTrackageRights(fromSubdivisionID: number): Promise<{
    data: SubdivisionTrackageRight[];
    errors: string[];
}> {
    try {
        if (!API_KEY) {
            return { data: [], errors: ['Client API key is not configured (VITE_API_KEY).'] };
        }

        const token = getAuthToken();
        const headers: HeadersInit = {
            'Content-Type': 'application/json',
            'X-Api-Key': String(API_KEY)
        };
        if (token) {
            headers['X-Auth-Token'] = token;
        }
        
        const response = await fetch(
            `${API_URL}/api/v1/SubdivisionTrackageRights/from/${fromSubdivisionID}`,
            { headers }
        );
        if (!response.ok) throw new Error('Failed to fetch trackage rights');
        const json = await response.json();
        return { data: json.data || [], errors: json.errors || [] };
    } catch (error) {
        return { data: [], errors: [String(error)] };
    }
}

export async function replaceTrackageRights(
    fromSubdivisionID: number,
    toSubdivisionIDs: number[]
): Promise<{
    success: boolean;
    errors: string[];
}> {
    try {
        if (!API_KEY) {
            return { success: false, errors: ['Client API key is not configured (VITE_API_KEY).'] };
        }

        const token = getAuthToken();
        const headers: HeadersInit = {
            'Content-Type': 'application/json',
            'X-Api-Key': String(API_KEY)
        };
        if (token) {
            headers['X-Auth-Token'] = token;
        }

        const response = await fetch(
            `${API_URL}/api/v1/SubdivisionTrackageRights/from/${fromSubdivisionID}`,
            {
                method: 'POST',
                headers,
                body: JSON.stringify(toSubdivisionIDs)
            }
        );
        if (!response.ok) throw new Error('Failed to update trackage rights');
        const json = await response.json();
        return { success: true, errors: json.errors || [] };
    } catch (error) {
        return { success: false, errors: [String(error)] };
    }
}

export async function deleteTrackageRight(id: number): Promise<{
    success: boolean;
    errors: string[];
}> {
    try {
        if (!API_KEY) {
            return { success: false, errors: ['Client API key is not configured (VITE_API_KEY).'] };
        }

        const token = getAuthToken();
        const headers: HeadersInit = {
            'Content-Type': 'application/json',
            'X-Api-Key': String(API_KEY)
        };
        if (token) {
            headers['X-Auth-Token'] = token;
        }

        const response = await fetch(
            `${API_URL}/api/v1/SubdivisionTrackageRights/${id}`,
            { method: 'DELETE', headers }
        );
        if (!response.ok) throw new Error('Failed to delete trackage right');
        return { success: true, errors: [] };
    } catch (error) {
        return { success: false, errors: [String(error)] };
    }
}
