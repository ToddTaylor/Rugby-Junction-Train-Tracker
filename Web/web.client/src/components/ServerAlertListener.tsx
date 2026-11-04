import { useCallback, useMemo, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useSignalR } from '../hooks/useSignalR';
import type { AuthSession } from '../types/Auth';
import type { ServerStatusChange } from '../types/ServerHealth';
import { parseSessionRoles } from '../utils/roles';
import { SERVER_STATUS_CHANGED_EVENT, describeStatusChange } from '../utils/serverHealth';
import './ServerAlertListener.css';

const SESSION_KEY = 'rjtt_auth_session';
const MAX_ALERTS = 4;
const RECOVERY_DISMISS_MS = 15_000;

type Alert = { id: string; change: ServerStatusChange; title: string; body: string };

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
 * Shows a pop-up on any page when a beacon PC goes offline, becomes degraded or recovers, for Admins and
 * Custodians. The session is read again on every navigation, so signing in or out starts or stops the alerts.
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

  const handlers = useMemo(() => ({
    ServerStatusChanged: (change: ServerStatusChange) => {
      window.dispatchEvent(new CustomEvent<ServerStatusChange>(SERVER_STATUS_CHANGED_EVENT, { detail: change }));
      if (change.alertsMuted) return;

      const alert: Alert = { id: `${change.beaconID}-${change.changedAt}`, change, ...describeStatusChange(change) };
      // A newer alert for the same server replaces the older one.
      setAlerts(current => [alert, ...current.filter(a => a.change.beaconID !== change.beaconID)].slice(0, MAX_ALERTS));
      if (change.status === 'Online') {
        window.setTimeout(() => dismiss(alert.id), RECOVERY_DISMISS_MS);
      }

      showDesktopNotification(alert, () => navigate(serverLink(change.beaconID)));
    },
  }), [dismiss, navigate]);

  useSignalR(handlers);

  if (alerts.length === 0) return null;

  return (
    <div className="server-alerts" aria-label="Server alerts">
      {alerts.map(alert => (
        <div
          key={alert.id}
          className={`server-alert ${alert.change.status.toLowerCase()}`}
          role={alert.change.status === 'Online' ? 'status' : 'alert'}
        >
          <span className="server-alert-dot" aria-hidden="true" />
          <div className="server-alert-text">
            <strong>{alert.title}</strong>
            <span>{alert.body}</span>
          </div>
          <div className="server-alert-actions">
            <Link to={serverLink(alert.change.beaconID)} onClick={() => dismiss(alert.id)}>View</Link>
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

  const notification = new Notification(alert.title, { body: alert.body, tag: `server-${alert.change.beaconID}` });
  notification.onclick = () => {
    window.focus();
    onOpen();
    notification.close();
  };
}

export default ServerAlertListener;
