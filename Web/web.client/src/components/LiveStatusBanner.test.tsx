import { act } from 'react';
import { createRoot, Root } from 'react-dom/client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import LiveStatusBanner from './LiveStatusBanner';
import { LIVE_STATUS_BANNER_DELAY_MS, LIVE_STATUS_BANNER_TEXT, type LiveConnectionStatus } from '../services/liveConnection';

describe('LiveStatusBanner', () => {
  let container: HTMLDivElement;
  let root: Root;

  function show(status: LiveConnectionStatus | null) {
    act(() => root.render(<LiveStatusBanner status={status} />));
  }

  function wait(ms: number) {
    act(() => { vi.advanceTimersByTime(ms); });
  }

  beforeEach(() => {
    (globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;
    vi.useFakeTimers();
    container = document.createElement('div');
    root = createRoot(container);
  });

  afterEach(() => {
    act(() => root.unmount());
    vi.useRealTimers();
  });

  it.each<LiveConnectionStatus | null>([null, 'connecting', 'connected'])('shows nothing while the status is %s', status => {
    show(status);
    wait(LIVE_STATUS_BANNER_DELAY_MS * 2);

    expect(container.textContent).toBe('');
  });

  it('appears once the connection has been down for a few seconds', () => {
    show('connected');
    show('reconnecting');

    wait(LIVE_STATUS_BANNER_DELAY_MS - 1);
    expect(container.textContent).toBe('');

    wait(1);
    expect(container.textContent).toBe(LIVE_STATUS_BANNER_TEXT);
    expect(container.querySelector('[role="status"]')).not.toBeNull();
  });

  it('stays hidden through a drop that recovers quickly', () => {
    show('reconnecting');
    wait(LIVE_STATUS_BANNER_DELAY_MS - 500);
    show('connected');
    wait(LIVE_STATUS_BANNER_DELAY_MS);

    expect(container.textContent).toBe('');
  });

  it('goes away as soon as the connection is back', () => {
    show('reconnecting');
    wait(LIVE_STATUS_BANNER_DELAY_MS);
    expect(container.textContent).toBe(LIVE_STATUS_BANNER_TEXT);

    show('connected');

    expect(container.textContent).toBe('');
  });
});
