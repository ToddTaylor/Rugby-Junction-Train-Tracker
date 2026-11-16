import { describe, expect, it, vi } from 'vitest';
import { RECONNECT_DELAYS_MS, nextReconnectDelay, reconnectPolicy, startWithRetry } from './liveConnection';

function retryContext(previousRetryCount: number) {
  return { previousRetryCount, elapsedMilliseconds: previousRetryCount * 30_000, retryReason: new Error('lost') };
}

describe('reconnect policy', () => {
  it('retries at once, then backs off', () => {
    expect([0, 1, 2, 3, 4].map(nextReconnectDelay)).toEqual(RECONNECT_DELAYS_MS);
  });

  it('never gives up: the last delay repeats however long the connection has been down', () => {
    // SignalR stops retrying when the policy returns null. Its default policy does so after four tries.
    for (const failedAttempts of [5, 6, 50, 5_000]) {
      expect(reconnectPolicy.nextRetryDelayInMilliseconds(retryContext(failedAttempts))).toBe(30_000);
    }
  });

  it('treats a negative count as the first attempt', () => {
    expect(nextReconnectDelay(-1)).toBe(0);
  });
});

describe('startWithRetry', () => {
  const noWait = () => Promise.resolve();

  it('reports no failed attempts when the first start works', async () => {
    const connection = { start: vi.fn().mockResolvedValue(undefined) };

    const failedAttempts = await startWithRetry(connection, { isDisposed: () => false, sleep: noWait });

    expect(failedAttempts).toBe(0);
    expect(connection.start).toHaveBeenCalledTimes(1);
  });

  it('keeps trying until the server is back, and reports how many attempts failed', async () => {
    // The page was loaded while the server was restarting.
    const connection = {
      start: vi.fn()
        .mockRejectedValueOnce(new Error('503'))
        .mockRejectedValueOnce(new Error('503'))
        .mockRejectedValueOnce(new Error('503'))
        .mockResolvedValue(undefined)
    };
    const onAttemptFailed = vi.fn();
    const sleep = vi.fn<(ms: number) => Promise<void>>(noWait);

    const failedAttempts = await startWithRetry(connection, { isDisposed: () => false, onAttemptFailed, sleep });

    expect(failedAttempts).toBe(3);
    expect(connection.start).toHaveBeenCalledTimes(4);
    expect(onAttemptFailed.mock.calls.map(call => call[1])).toEqual([1, 2, 3]);
    expect(sleep.mock.calls.map(call => call[0])).toEqual([2_000, 5_000, 10_000]);
  });

  it('stops retrying once the owner has gone away', async () => {
    let disposed = false;
    const connection = { start: vi.fn().mockRejectedValue(new Error('down')) };
    const sleep = vi.fn(async () => { disposed = connection.start.mock.calls.length >= 2; });

    const failedAttempts = await startWithRetry(connection, { isDisposed: () => disposed, sleep });

    expect(failedAttempts).toBeNull();
    expect(connection.start).toHaveBeenCalledTimes(2);
  });

  it('reports nothing when the owner went away while the connection was opening', async () => {
    let disposed = false;
    const connection = { start: vi.fn(async () => { disposed = true; }) };

    const failedAttempts = await startWithRetry(connection, { isDisposed: () => disposed, sleep: noWait });

    expect(failedAttempts).toBeNull();
  });

  it('does not start a connection for an owner that is already gone', async () => {
    const connection = { start: vi.fn() };

    const failedAttempts = await startWithRetry(connection, { isDisposed: () => true, sleep: noWait });

    expect(failedAttempts).toBeNull();
    expect(connection.start).not.toHaveBeenCalled();
  });
});
