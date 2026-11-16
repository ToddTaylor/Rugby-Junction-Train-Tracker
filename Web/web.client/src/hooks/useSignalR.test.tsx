import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { useSignalR } from './useSignalR';
import { reconnectPolicy, type LiveConnectionStatus } from '../services/liveConnection';
import { getAuthToken } from '../services/auth';

/** A stand-in for a SignalR HubConnection that records what the hook registers and lets a test fire it. */
class FakeConnection {
  handlers = new Map<string, (payload: unknown) => void>();
  closed?: (error?: Error) => void;
  reconnecting?: (error?: Error) => void;
  reconnected?: (connectionId?: string) => void;
  start = vi.fn<() => Promise<void>>().mockResolvedValue(undefined);
  stop = vi.fn<() => Promise<void>>().mockResolvedValue(undefined);
  on(name: string, handler: (payload: unknown) => void) { this.handlers.set(name, handler); }
  off(name: string) { this.handlers.delete(name); }
  onclose(callback: (error?: Error) => void) { this.closed = callback; }
  onreconnecting(callback: (error?: Error) => void) { this.reconnecting = callback; }
  onreconnected(callback: (connectionId?: string) => void) { this.reconnected = callback; }
}

const fake = vi.hoisted(() => ({
  connections: [] as unknown[],
  policy: undefined as unknown,
  prepare: undefined as ((connection: unknown) => void) | undefined,
}));

vi.mock('@microsoft/signalr', () => ({
  HttpTransportType: { WebSockets: 1 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect(policy: unknown) { fake.policy = policy; return this; }
    build() {
      const connection = new FakeConnection();
      fake.prepare?.(connection);
      fake.connections.push(connection);
      return connection;
    }
  },
}));

vi.mock('../services/auth', () => ({
  getAuthToken: vi.fn(),
}));

const mockedGetAuthToken = vi.mocked(getAuthToken);

let root: Root;
let status: LiveConnectionStatus | null;
let onReconnected: ReturnType<typeof vi.fn>;
let onMapPinRemoved: ReturnType<typeof vi.fn>;

function Harness() {
  status = useSignalR({ Reconnected: onReconnected, MapPinRemoved: onMapPinRemoved }).status;
  return null;
}

/** Mounts the hook and lets its start-up, which is asynchronous, run to the end. */
async function mount() {
  await act(async () => {
    root.render(<Harness />);
  });
  await act(async () => {
    await vi.advanceTimersByTimeAsync(0);
  });
}

function connection(index = 0): FakeConnection {
  return fake.connections[index] as FakeConnection;
}

describe('useSignalR', () => {
  beforeEach(() => {
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
    vi.useFakeTimers();
    vi.spyOn(console, 'log').mockImplementation(() => undefined);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    fake.connections.length = 0;
    fake.policy = undefined;
    fake.prepare = undefined;
    mockedGetAuthToken.mockReset().mockResolvedValue('token');
    onReconnected = vi.fn();
    onMapPinRemoved = vi.fn();
    status = null;
    root = createRoot(document.createElement('div'));
  });

  afterEach(() => {
    act(() => root.unmount());
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('connects with the policy that never stops retrying', async () => {
    await mount();

    expect(fake.policy).toBe(reconnectPolicy);
    expect(connection().start).toHaveBeenCalledTimes(1);
    expect(status).toBe('connected');
    expect(onReconnected).not.toHaveBeenCalled();
  });

  it('opens no connection when nobody is signed in', async () => {
    mockedGetAuthToken.mockResolvedValue(null);

    await mount();

    expect(fake.connections).toHaveLength(0);
    expect(status).toBeNull();
  });

  it('reports the drop, and asks for a refresh when the connection is back', async () => {
    await mount();

    act(() => connection().reconnecting?.(new Error('network lost')));
    expect(status).toBe('reconnecting');
    expect(onReconnected).not.toHaveBeenCalled();

    act(() => connection().reconnected?.('new-id'));
    expect(status).toBe('connected');
    expect(onReconnected).toHaveBeenCalledTimes(1);
  });

  it('keeps trying when the first attempt fails, then asks for a refresh', async () => {
    // The page was opened while the server was restarting: data fetched at load may be missing too.
    fake.prepare = c => {
      (c as FakeConnection).start.mockRejectedValueOnce(new Error('503')).mockRejectedValueOnce(new Error('503'));
    };

    await mount();
    expect(status).toBe('reconnecting');
    expect(onReconnected).not.toHaveBeenCalled();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(60_000);
    });

    expect(connection().start).toHaveBeenCalledTimes(3);
    expect(status).toBe('connected');
    expect(onReconnected).toHaveBeenCalledTimes(1);
  });

  it('opens the connection again when it closes for good', async () => {
    await mount();

    await act(async () => {
      connection().closed?.(new Error('server closed the connection'));
      await vi.advanceTimersByTimeAsync(0);
    });

    expect(connection().start).toHaveBeenCalledTimes(2);
    expect(status).toBe('connected');
    expect(onReconnected).toHaveBeenCalledTimes(1);
  });

  it('still delivers messages to the handlers', async () => {
    await mount();

    act(() => connection().handlers.get('MapPinRemoved')?.({ mapPinId: '17' }));

    expect(onMapPinRemoved).toHaveBeenCalledWith(17);
  });

  it('stops the connection on unmount and does not reopen it', async () => {
    await mount();
    const opened = connection();

    act(() => root.unmount());
    await act(async () => {
      opened.closed?.();
      await vi.advanceTimersByTimeAsync(60_000);
    });

    expect(opened.stop).toHaveBeenCalledTimes(1);
    expect(opened.start).toHaveBeenCalledTimes(1);
    root = createRoot(document.createElement('div'));
  });

  it('opens no connection when unmounted before the sign-in token was read', async () => {
    let resolveToken: (token: string) => void = () => undefined;
    mockedGetAuthToken.mockReturnValue(new Promise<string>(resolve => { resolveToken = resolve; }));

    await act(async () => {
      root.render(<Harness />);
    });
    act(() => root.unmount());
    await act(async () => {
      resolveToken('token');
      await vi.advanceTimersByTimeAsync(0);
    });

    expect(fake.connections).toHaveLength(0);
    root = createRoot(document.createElement('div'));
  });
});
