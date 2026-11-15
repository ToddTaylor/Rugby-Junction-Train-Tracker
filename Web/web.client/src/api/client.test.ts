import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fetchApi, getAuthToken } from './client';
import { deleteUser } from './users';
import { createBeacon } from './beacons';
import { deleteRailroad } from './railroads';
import { replaceTrackageRights } from './subdivisionTrackageRights';
import { checkSession } from './auth';
import { fetchWithAuth } from '../utils/fetchWithAuth';

vi.mock('../utils/fetchWithAuth', () => ({
  fetchWithAuth: vi.fn(),
}));

const mockedFetchWithAuth = vi.mocked(fetchWithAuth);

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  });
}

function signIn(token = 'token-123') {
  localStorage.setItem('rjtt_auth_session', JSON.stringify({ token, roles: ['Admin'] }));
}

function sentHeaders(call = 0): Record<string, string> {
  return mockedFetchWithAuth.mock.calls[call][1]?.headers as Record<string, string>;
}

describe('api client', () => {
  beforeEach(() => {
    mockedFetchWithAuth.mockReset();
    vi.unstubAllGlobals();
    localStorage.clear();
    sessionStorage.clear();
  });

  it('reads the token from the session useAuth stores in localStorage', () => {
    expect(getAuthToken()).toBeNull();

    signIn('abc');

    expect(getAuthToken()).toBe('abc');
  });

  it('returns null for a session that cannot be parsed', () => {
    localStorage.setItem('rjtt_auth_session', 'not json');

    expect(getAuthToken()).toBeNull();
  });

  it('sends the sign-in token with every request', async () => {
    signIn();
    mockedFetchWithAuth.mockResolvedValue(jsonResponse({ data: [1], errors: [] }));

    const result = await fetchApi<number[]>('/api/v1/Users');

    expect(sentHeaders()['X-Auth-Token']).toBe('token-123');
    expect(result).toEqual({ data: [1], errors: [] });
  });

  it('sends no token header when nobody is signed in', async () => {
    mockedFetchWithAuth.mockResolvedValue(jsonResponse({ data: [], errors: [] }));

    await fetchApi('/api/v1/Railroads');

    expect(sentHeaders()).not.toHaveProperty('X-Auth-Token');
  });

  it('returns the server message when the role is missing', async () => {
    signIn();
    mockedFetchWithAuth.mockResolvedValue(
      jsonResponse({ data: null, errors: ['You do not have permission to do this.'] }, 403));

    const result = await fetchApi('/api/v1/Users');

    expect(result).toEqual({ data: null, errors: ['You do not have permission to do this.'] });
  });

  it('reports a failure that has no body', async () => {
    mockedFetchWithAuth.mockResolvedValue(new Response(null, { status: 500 }));

    const result = await fetchApi('/api/v1/Users');

    expect(result).toEqual({ data: null, errors: ['Request failed'] });
  });

  it.each([
    ['deleteUser', () => deleteUser(4)],
    ['createBeacon', () => createBeacon({} as Parameters<typeof createBeacon>[0])],
    ['deleteRailroad', () => deleteRailroad(2)],
  ])('%s sends the sign-in token', async (_name, call) => {
    signIn();
    mockedFetchWithAuth.mockResolvedValue(new Response(null, { status: 204 }));

    await call();

    expect(sentHeaders()['X-Auth-Token']).toBe('token-123');
  });

  it('deleteUser reports the refusal from the server', async () => {
    signIn();
    mockedFetchWithAuth.mockResolvedValue(
      jsonResponse({ data: null, errors: ['Only an Admin can change or delete an Admin user.'] }, 403));

    const result = await deleteUser(1);

    expect(result).toEqual({ success: false, errors: ['Only an Admin can change or delete an Admin user.'] });
  });

  it('replaceTrackageRights sends the sign-in token', async () => {
    signIn();
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ data: { success: true }, errors: [] }));
    vi.stubGlobal('fetch', fetchMock);

    const result = await replaceTrackageRights(1, [2, 3]);

    expect(result.success).toBe(true);
    expect(fetchMock.mock.calls[0][1].headers['X-Auth-Token']).toBe('token-123');
  });

  it('checkSession asks the server about the stored token', async () => {
    signIn();
    mockedFetchWithAuth.mockResolvedValue(jsonResponse({ authenticated: true }));

    await checkSession();

    expect(String(mockedFetchWithAuth.mock.calls[0][0])).toContain('/api/v1/auth/session');
    expect(sentHeaders()['X-Auth-Token']).toBe('token-123');
  });

  it('checkSession does nothing when nobody is signed in', async () => {
    await checkSession();

    expect(mockedFetchWithAuth).not.toHaveBeenCalled();
  });
});
