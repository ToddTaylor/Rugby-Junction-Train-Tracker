import { Subdivision, CreateSubdivision, UpdateSubdivision } from '../types/Subdivision';
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

interface ApiResponse<T> {
  data: T | null;
  errors: string[];
}

function parseLocalTrainAddressIDs(raw: string | undefined): number[] {
  if (!raw || !raw.trim()) {
    return [];
  }

  return raw
    .split(/[\r\n,]+/)
    .map(value => value.trim())
    .filter(value => /^\d+$/.test(value))
    .map(value => Number.parseInt(value, 10));
}

function serializeLocalTrainAddressIDs(values: number[]): string {
  if (values.length === 0) {
    return '';
  }

  return values
    .filter(value => Number.isInteger(value) && value > 0)
    .sort((a, b) => a - b)
    .join('\n');
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

export interface ToggleSubdivisionLocalTrainResult {
  isLocal: boolean;
  subdivision: Subdivision;
}

export async function toggleSubdivisionLocalTrainAddress(
  subdivisionId: number,
  addressId: number,
  options: { isAdmin: boolean; currentUserId?: number | null }
): Promise<ApiResponse<ToggleSubdivisionLocalTrainResult>> {
  if (!Number.isInteger(subdivisionId) || subdivisionId <= 0) {
    return { data: null, errors: ['Invalid subdivision ID.'] };
  }

  if (!Number.isInteger(addressId) || addressId <= 0) {
    return { data: null, errors: ['Invalid address ID.'] };
  }

  const subdivisionResult = await getSubdivisionById(subdivisionId);
  if (subdivisionResult.errors.length > 0 || !subdivisionResult.data) {
    return {
      data: null,
      errors: subdivisionResult.errors.length > 0
        ? subdivisionResult.errors
        : ['Subdivision not found.']
    };
  }

  const subdivision = subdivisionResult.data;
  const { isAdmin, currentUserId = null } = options;
  if (!isAdmin && (currentUserId === null || subdivision.custodianId !== currentUserId)) {
    return { data: null, errors: ['You can only modify local trains for your assigned subdivision.'] };
  }

  const ids = new Set(parseLocalTrainAddressIDs(subdivision.localTrainAddressIDs));
  const wasLocal = ids.has(addressId);
  if (wasLocal) {
    ids.delete(addressId);
  } else {
    ids.add(addressId);
  }

  const updateData: UpdateSubdivision = {
    id: subdivision.id,
    name: subdivision.name,
    railroadID: subdivision.railroadID,
    dpuCapable: subdivision.dpuCapable,
    localTrainAddressIDs: serializeLocalTrainAddressIDs(Array.from(ids)),
    custodianId: subdivision.custodianId ?? null,
  };

  const updateResult = await updateSubdivision(subdivision.id, updateData);
  if (updateResult.errors.length > 0 || !updateResult.data) {
    return {
      data: null,
      errors: updateResult.errors.length > 0
        ? updateResult.errors
        : ['Failed to update subdivision local train list.']
    };
  }

  return {
    data: {
      isLocal: !wasLocal,
      subdivision: updateResult.data,
    },
    errors: []
  };
}
