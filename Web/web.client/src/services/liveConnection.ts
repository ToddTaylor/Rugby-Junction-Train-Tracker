import type { IRetryPolicy } from '@microsoft/signalr';

/**
 * State of the SignalR connection that carries live map updates.
 * - connecting: the first attempt is under way.
 * - connected: updates are arriving.
 * - reconnecting: the connection was lost, or never opened, and is being retried. Data on screen may be stale.
 */
export type LiveConnectionStatus = 'connecting' | 'connected' | 'reconnecting';

/** True when the status means updates are not arriving. */
export function isLiveConnectionDown(status: LiveConnectionStatus | null): boolean {
  return status === 'reconnecting';
}

/** A drop shorter than this is not worth telling the user about; most reconnects finish sooner. */
export const LIVE_STATUS_BANNER_DELAY_MS = 3_000;

export const LIVE_STATUS_BANNER_TEXT = 'Live updates paused. Reconnecting…';

/** Waits before each retry. The last value repeats for as long as the connection stays down. */
export const RECONNECT_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000];

/**
 * How long to wait before the next retry, given how many have failed so far. Never gives up: a map left
 * open through a server deploy or a long network drop must come back on its own.
 */
export function nextReconnectDelay(failedAttempts: number): number {
  const index = Math.min(Math.max(failedAttempts, 0), RECONNECT_DELAYS_MS.length - 1);
  return RECONNECT_DELAYS_MS[index];
}

/**
 * Retry policy for withAutomaticReconnect. SignalR's default policy stops after four tries (about 42
 * seconds), after which the map would show frozen data until the page was reloaded.
 */
export const reconnectPolicy: IRetryPolicy = {
  nextRetryDelayInMilliseconds: context => nextReconnectDelay(context.previousRetryCount),
};

export interface StartRetryOptions {
  /** True once the owner no longer wants the connection; stops the retries. */
  isDisposed: () => boolean;
  /** Called after each failed attempt, before waiting. */
  onAttemptFailed?: (error: unknown, failedAttempts: number) => void;
  /** Waits the given time. Replaced in tests. */
  sleep?: (ms: number) => Promise<void>;
}

const defaultSleep = (ms: number) => new Promise<void>(resolve => setTimeout(resolve, ms));

/**
 * Starts a connection, retrying until it opens. withAutomaticReconnect only covers a connection that was
 * open and then dropped; it does nothing when the first start fails, for example when the page loads
 * while the server is restarting.
 *
 * Returns how many attempts failed before the one that worked, or null when the owner disposed first.
 */
export async function startWithRetry(
  connection: { start(): Promise<void> },
  { isDisposed, onAttemptFailed, sleep = defaultSleep }: StartRetryOptions
): Promise<number | null> {
  let failedAttempts = 0;

  while (!isDisposed()) {
    try {
      await connection.start();
      return isDisposed() ? null : failedAttempts;
    } catch (error) {
      failedAttempts++;
      onAttemptFailed?.(error, failedAttempts);
      await sleep(nextReconnectDelay(failedAttempts));
    }
  }

  return null;
}
