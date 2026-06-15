// Centralized fetch utility for API calls with automatic 401 logout handling

const STORAGE_KEY = 'rjtt_auth_session';

export async function fetchWithAuth(input: RequestInfo, init?: RequestInit): Promise<Response> {
  const response = await fetch(input, init);
  if (response.status === 401) {
    localStorage.removeItem(STORAGE_KEY);
    window.location.href = '/login?blocked=1';
  }
  return response;
}
