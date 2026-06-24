import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { Subdivision } from '../types/Subdivision';
import { toggleSubdivisionLocalTrainAddress } from './subdivisions';
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

function createSubdivision(overrides: Partial<Subdivision> = {}): Subdivision {
  return {
    id: 5,
    railroadID: 1,
    railroad: 'CN',
    dpuCapable: true,
    name: 'Waukesha',
    localTrainAddressIDs: '101\n202',
    createdAt: '2026-01-01T00:00:00Z',
    lastUpdate: '2026-01-02T00:00:00Z',
    custodianId: 7,
    ...overrides,
  };
}

describe('toggleSubdivisionLocalTrainAddress', () => {
  beforeEach(() => {
    mockedFetchWithAuth.mockReset();
    localStorage.clear();
    sessionStorage.clear();
  });

  it('adds address ID when it is not already local', async () => {
    const subdivision = createSubdivision({ localTrainAddressIDs: '101\n202' });
    const updatedSubdivision = createSubdivision({ localTrainAddressIDs: '101\n202\n303' });

    mockedFetchWithAuth.mockResolvedValueOnce(
      jsonResponse({ data: subdivision, errors: [] })
    );
    mockedFetchWithAuth.mockResolvedValueOnce(
      jsonResponse({ data: updatedSubdivision, errors: [] })
    );

    const result = await toggleSubdivisionLocalTrainAddress(5, 303, {
      isAdmin: true,
      currentUserId: null,
    });

    expect(result.errors).toEqual([]);
    expect(result.data?.isLocal).toBe(true);
    expect(mockedFetchWithAuth).toHaveBeenCalledTimes(2);

    const [, updateInit] = mockedFetchWithAuth.mock.calls[1];
    const updateBody = JSON.parse(String(updateInit?.body)) as { localTrainAddressIDs: string };
    expect(updateBody.localTrainAddressIDs).toBe('101\n202\n303');
  });

  it('removes address ID when it is already local', async () => {
    const subdivision = createSubdivision({ localTrainAddressIDs: '303,101' });
    const updatedSubdivision = createSubdivision({ localTrainAddressIDs: '101' });

    mockedFetchWithAuth.mockResolvedValueOnce(
      jsonResponse({ data: subdivision, errors: [] })
    );
    mockedFetchWithAuth.mockResolvedValueOnce(
      jsonResponse({ data: updatedSubdivision, errors: [] })
    );

    const result = await toggleSubdivisionLocalTrainAddress(5, 303, {
      isAdmin: false,
      currentUserId: 7,
    });

    expect(result.errors).toEqual([]);
    expect(result.data?.isLocal).toBe(false);
    expect(mockedFetchWithAuth).toHaveBeenCalledTimes(2);

    const [, updateInit] = mockedFetchWithAuth.mock.calls[1];
    const updateBody = JSON.parse(String(updateInit?.body)) as { localTrainAddressIDs: string };
    expect(updateBody.localTrainAddressIDs).toBe('101');
  });

  it('returns forbidden error for non-assigned custodian', async () => {
    const subdivision = createSubdivision({ custodianId: 7 });

    mockedFetchWithAuth.mockResolvedValueOnce(
      jsonResponse({ data: subdivision, errors: [] })
    );

    const result = await toggleSubdivisionLocalTrainAddress(5, 303, {
      isAdmin: false,
      currentUserId: 8,
    });

    expect(result.data).toBeNull();
    expect(result.errors).toContain('You can only modify local trains for your assigned subdivision.');
    expect(mockedFetchWithAuth).toHaveBeenCalledTimes(1);
  });

  it('returns validation error and skips network for invalid IDs', async () => {
    const result = await toggleSubdivisionLocalTrainAddress(0, -1, {
      isAdmin: true,
      currentUserId: null,
    });

    expect(result.data).toBeNull();
    expect(result.errors).toContain('Invalid subdivision ID.');
    expect(mockedFetchWithAuth).not.toHaveBeenCalled();
  });
});
