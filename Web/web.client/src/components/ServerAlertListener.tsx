import { useCallback, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useSignalR } from '../hooks/useSignalR';
import type { AuthSession } from '../types/Auth';
import type { ServerCommandUpdate, ServerStatusChange } from '../types/ServerHealth';
import { parseSessionRoles } from '../utils/roles';
import {
  AlertTone,
  SERVER_COMMAND_UPDATED_EVENT,
  SERVER_STATUS_CHANGED_EVENT,
  describeCommandUpdate,
  describeStatusChange,
} from '../utils/serverHealth';
import './ServerAlertListener.css';

const SESSION_KEY = 'rjtt_auth_session';
const MAX_ALERTS = 4;
const AUTO_DISMISS_MS = 15_000;

type Alert = { id: string; beaconId: number; kind: 'status' | 'command'; tone: AlertTone; title: string; body: string };

/** The signed-in Admin's or Custodian's token, or null for anyone else. */
function readSupportToken(): string | null {
  try {
    const session = JSON.parse(localStorage.getItem(SESSION_KEY) ?? 'null') as AuthSession | null;
    if (!session?.token || new Date(session.expiresUtc).getTime() < Date.now()) return null;
    const { isAdmin, isCustodian } = parseSessionRoles(session.roles);
    return isAdmin || isCustodian ? session.token : null;
  } catch {
    return null;
  }
}

/**
 * Shows a pop-up on any page when a beacon PC goes offline, becomes degraded or recovers, and when a reboot or
 * restart-apps command is requested or reported on, for Admins and Custodians. The session is read again on every
 * navigation, so signing in or out starts or stops the alerts.
 */
const ServerAlertListener = () => {
  const location = useLocation();
  const token = useMemo(readSupportToken, [location.key]);
  return token ? <ServerAlerts key={token} /> : null;
};

const ServerAlerts = () => {
  const [alerts, setAlerts] = useState<Alert[]>([]);
  const navigate = useNavigate();

  const dismiss = useCallback((id: string) => setAlerts(current => current.filter(a => a.id !== id)), []);

  const show = useCallback((alert: Alert, autoDismiss: boolean) => {
    // A newer alert of the same kind for the same server replaces the older one.
    setAlerts(current => [alert, ...current.filter(a => a.beaconId !== alert.beaconId || a.kind !== alert.kind)].slice(0, MAX_ALERTS));
    if (autoDismiss) {
      window.setTimeout(() => dismiss(alert.id), AUTO_DISMISS_MS);
    }
    showDesktopNotification(alert, () => navigate(serverLink(alert.beaconId)));
  }, [dismiss, navigate]);

  const handlers = useMemo(() => ({
    ServerStatusChanged: (change: ServerStatusChange) => {
      window.dispatchEvent(new CustomEvent<ServerStatusChange>(SERVER_STATUS_CHANGED_EVENT, { detail: change }));
      if (change.alertsMuted) return;

      const tone: AlertTone = change.status === 'Online' ? 'online' : change.status === 'Degraded' ? 'degraded' : 'offline';
      show(
        { id: `status-${change.beaconID}-${change.changedAt}`, beaconId: change.beaconID, kind: 'status', tone, ...describeStatusChange(change) },
        change.status === 'Online');
    },
    ServerCommandUpdated: (update: ServerCommandUpdate) => {
      window.dispatchEvent(new CustomEvent<ServerCommandUpdate>(SERVER_COMMAND_UPDATED_EVENT, { detail: update }));

      const { title, body, tone } = describeCommandUpdate(update);
      show(
        { id: `command-${update.command.id}-${update.command.status}`, beaconId: update.beaconID, kind: 'command', tone, title, body },
        tone === 'info' || tone === 'online');
    },
  }), [show]);

  useSignalR(handlers);

  if (alerts.length === 0) return null;

  return (
    <div className="server-alerts" aria-label="Server alerts">
      {alerts.map(alert => (
        <div
          key={alert.id}
          className={`server-alert ${alert.tone}`}
          role={alert.tone === 'online' || alert.tone === 'info' ? 'status' : 'alert'}
        >
          <span className="server-alert-dot" aria-hidden="true" />
          <div className="server-alert-text">
            <strong>{alert.title}</strong>
            <span>{alert.body}</span>
          </div>
          <div className="server-alert-actions">
            <Link to={serverLink(alert.beaconId)} onClick={() => dismiss(alert.id)}>View</Link>
            <button type="button" onClick={() => dismiss(alert.id)} aria-label={`Dismiss ${alert.title}`}>✕</button>
          </div>
        </div>
      ))}
    </div>
  );
};

const serverLink = (beaconId: number) => `/admin/servers?beacon=${beaconId}`;

/** Raises a Windows notification too when the page is in a background tab and the user has allowed it. */
function showDesktopNotification(alert: Alert, onOpen: () => void) {
  if (typeof Notification === 'undefined' || Notification.permission !== 'granted' || !document.hidden) return;

  const notification = new Notification(alert.title, { body: alert.body, tag: `server-${alert.kind}-${alert.beaconId}` });
  notification.onclick = () => {
    window.focus();
    onOpen();
    notification.close();
  };
}

export default ServerAlertListener;
