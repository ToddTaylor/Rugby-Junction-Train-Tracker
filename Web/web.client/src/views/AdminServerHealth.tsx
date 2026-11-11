import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { BeaconCommandType, ServerHealth, ServerStatus } from '../types/ServerHealth';
import {
  cancelServerCommand,
  createDeviceKey,
  getServerHealth,
  muteServerAlerts,
  removeDeviceKey,
  requestServerCommand,
  unmuteServerAlerts,
  updateServerExpectations,
} from '../api/serverHealth';
import AdminPageHeader from '../components/admin/AdminPageHeader';
import {
  MUTE_DURATIONS,
  MuteDuration,
  SERVER_COMMAND_UPDATED_EVENT,
  SERVER_STATUS_CHANGED_EVENT,
  ServerHealthFilter,
  activeCommand,
  commandAvailability,
  commandLabel,
  commandStatusLabel,
  countByStatus,
  describeMute,
  filterServers,
  formatGigabytes,
  formatProgramCount,
  formatTaskResult,
  formatTimeAgo,
  formatUptime,
  isProgramInSummary,
  isProgramShort,
  muteUntilFor,
  parseExpectedCount,
  sortServers,
} from '../utils/serverHealth';
import './AdminSkin.css';
import './AdminServerHealth.css';

const REFRESH_INTERVAL_MS = 60_000;

const STATUS_CLASS: Record<ServerStatus, string> = {
  Online: 'online',
  Degraded: 'degraded',
  Offline: 'offline',
};

const StatusChip = ({ status }: { status: ServerStatus }) => (
  <span className={`server-status-chip ${STATUS_CLASS[status]}`}>
    <span className="server-status-dot" aria-hidden="true" />
    {status}
  </span>
);

type ExpectationsForm = { sdrSharp: string; sdrSlices: string; softEot: string; softDpu: string };

type CommandConfirm = { server: ServerHealth; type: BeaconCommandType };

/** 'confirm' asks before creating or replacing a key; 'show' displays the new key, which is never shown again. */
type KeyDialog = { server: ServerHealth; stage: 'confirm' | 'show' | 'remove'; key?: string };

const toField = (value: number | null) => (value === null ? '' : String(value));

/** The API address the PC's ConsoleApp subscriber for this site sends to, as its settings prompt shows it. */
const apiUrl = `${import.meta.env.VITE_API_URL || window.location.origin}/api/v1`;

const formatDateTime = (iso: string) => new Date(iso).toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });

const AdminServerHealth = () => {
  const [servers, setServers] = useState<ServerHealth[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [filter, setFilter] = useState<ServerHealthFilter>('all');
  const [searchParams, setSearchParams] = useSearchParams();
  // ?beacon=<id> opens that server's details, so other pages can link straight to it.
  const selectedId = Number(searchParams.get('beacon')) || null;
  const [editing, setEditing] = useState<ServerHealth | null>(null);
  const [form, setForm] = useState<ExpectationsForm>({ sdrSharp: '', sdrSlices: '', softEot: '', softDpu: '' });
  const [formError, setFormError] = useState<string>();
  const [now, setNow] = useState(() => new Date());
  const [muting, setMuting] = useState<ServerHealth | null>(null);
  const [muteDuration, setMuteDuration] = useState<MuteDuration>('day');
  const [muteNote, setMuteNote] = useState('');
  const [muteError, setMuteError] = useState<string>();
  const [notificationPermission, setNotificationPermission] = useState(() =>
    typeof Notification === 'undefined' ? 'unsupported' : Notification.permission);
  const [commandConfirm, setCommandConfirm] = useState<CommandConfirm | null>(null);
  const [confirmName, setConfirmName] = useState('');
  const [commandError, setCommandError] = useState<string>();
  const [keyDialog, setKeyDialog] = useState<KeyDialog | null>(null);
  const [keyError, setKeyError] = useState<string>();
  const [keyCopied, setKeyCopied] = useState(false);
  const [busy, setBusy] = useState(false);

  const loadServers = useCallback(async () => {
    const response = await getServerHealth();
    if (response.errors.length > 0) {
      setError(response.errors.join(', '));
    } else {
      setError(undefined);
      setServers(response.data ?? []);
    }
    setNow(new Date());
    setLoading(false);
  }, []);

  useEffect(() => {
    loadServers();
    const timer = window.setInterval(loadServers, REFRESH_INTERVAL_MS);
    return () => window.clearInterval(timer);
  }, [loadServers]);

  // The app-wide alert listener announces each status change and command update, so the table updates without
  // waiting for the timer.
  useEffect(() => {
    const refresh = () => { loadServers(); };
    window.addEventListener(SERVER_STATUS_CHANGED_EVENT, refresh);
    window.addEventListener(SERVER_COMMAND_UPDATED_EVENT, refresh);
    return () => {
      window.removeEventListener(SERVER_STATUS_CHANGED_EVENT, refresh);
      window.removeEventListener(SERVER_COMMAND_UPDATED_EVENT, refresh);
    };
  }, [loadServers]);

  const openCommand = (server: ServerHealth, type: BeaconCommandType) => {
    setCommandConfirm({ server, type });
    setConfirmName('');
    setCommandError(undefined);
  };

  const handleCommand = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!commandConfirm || busy) return;

    setBusy(true);
    const response = await requestServerCommand(commandConfirm.server.beaconID, commandConfirm.type);
    setBusy(false);
    if (response.errors.length > 0) {
      setCommandError(response.errors.join(', '));
      return;
    }

    setCommandConfirm(null);
    await loadServers();
  };

  const handleCancelCommand = async (server: ServerHealth, commandId: number) => {
    const response = await cancelServerCommand(server.beaconID, commandId);
    if (response.errors.length > 0) {
      setError(response.errors.join(', '));
    }
    await loadServers();
  };

  const openKeyDialog = (server: ServerHealth, stage: 'confirm' | 'remove') => {
    setKeyDialog({ server, stage });
    setKeyError(undefined);
    setKeyCopied(false);
  };

  const handleCreateKey = async () => {
    if (!keyDialog || busy) return;

    setBusy(true);
    const response = await createDeviceKey(keyDialog.server.beaconID);
    setBusy(false);
    if (response.errors.length > 0 || !response.data) {
      setKeyError(response.errors.join(', ') || 'The key could not be created.');
      return;
    }

    setKeyDialog({ ...keyDialog, stage: 'show', key: response.data.deviceKey });
    await loadServers();
  };

  const handleRemoveKey = async () => {
    if (!keyDialog || busy) return;

    setBusy(true);
    const response = await removeDeviceKey(keyDialog.server.beaconID);
    setBusy(false);
    if (response.errors.length > 0) {
      setKeyError(response.errors.join(', '));
      return;
    }

    setKeyDialog(null);
    await loadServers();
  };

  const copyKey = async () => {
    if (!keyDialog?.key) return;
    try {
      await navigator.clipboard.writeText(keyDialog.key);
      setKeyCopied(true);
    } catch {
      setKeyError('Copying is blocked in this browser. Select the key and copy it by hand.');
    }
  };

  const enableDesktopAlerts = async () => {
    setNotificationPermission(await Notification.requestPermission());
  };

  const openMute = (server: ServerHealth) => {
    setMuting(server);
    setMuteDuration('day');
    setMuteNote('');
    setMuteError(undefined);
  };

  const handleMute = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!muting) return;

    const note = muteNote.trim();
    const response = await muteServerAlerts(muting.beaconID, {
      mutedUntil: muteUntilFor(muteDuration),
      note: note === '' ? null : note,
    });
    if (response.errors.length > 0) {
      setMuteError(response.errors.join(', '));
      return;
    }

    setMuting(null);
    await loadServers();
  };

  const handleUnmute = async (server: ServerHealth) => {
    const response = await unmuteServerAlerts(server.beaconID);
    if (response.errors.length > 0) {
      setError(response.errors.join(', '));
      return;
    }
    await loadServers();
  };

  const renderMuteButton = (server: ServerHealth) => server.alertsMuted
    ? <button className="btn-secondary" onClick={() => handleUnmute(server)}>Turn alerts on</button>
    : <button className="btn-secondary" onClick={() => openMute(server)}>Mute alerts</button>;

  const renderRemoteSection = (server: ServerHealth) => {
    const reboot = commandAvailability(server, 'Reboot');
    const restartApps = commandAvailability(server, 'RestartApps');
    const active = activeCommand(server);
    const keyRefused = server.deviceKeyRejectedAt && server.deviceKeySetAt
      && new Date(server.deviceKeyRejectedAt) > new Date(server.deviceKeySetAt);
    // Explain why the buttons are off once, preferring the reboot reason.
    const reason = reboot.reason ?? restartApps.reason;
    const showRestartApps = server.diagnostics?.restartAppsAvailable ?? false;

    return (
      <section className="server-drawer-section">
        <h3>Remote restart</h3>
        {keyRefused && (
          <div className="server-reason offline">
            A check-in was refused {formatTimeAgo(server.deviceKeyRejectedAt, now)} because the PC sent a missing or wrong device key.
          </div>
        )}
        <div className="server-drawer-alerts">
          <div>
            <div>Device key</div>
            <div className="server-subtext">
              {server.deviceKeySetAt
                ? `Created ${formatDateTime(server.deviceKeySetAt)}${server.deviceKeySetBy ? ` by ${server.deviceKeySetBy}` : ''}`
                : 'None yet. The PC cannot be restarted from here until it has one.'}
            </div>
            <div className="server-subtext">
              On the PC: remote commands {server.diagnostics?.remoteCommandsEnabled ? 'allowed' : 'off'}
              {server.diagnostics?.remoteCommandsEnabled ? (showRestartApps ? ' · restart apps command set up' : ' · no restart apps command') : ''}
            </div>
          </div>
          <div className="server-actions">
            <button className="btn-secondary" onClick={() => openKeyDialog(server, 'confirm')}>
              {server.deviceKeySetAt ? 'Replace key' : 'Create device key'}
            </button>
            {server.deviceKeySetAt && (
              <button className="btn-secondary" onClick={() => openKeyDialog(server, 'remove')}>Remove key</button>
            )}
          </div>
        </div>

        {active && (
          <div className="server-command-active">
            <div>
              <strong>{commandLabel(active.type)}: {commandStatusLabel(active)}</strong>
              <div className="server-subtext">
                Requested by {active.requestedBy} {formatTimeAgo(active.requestedAt, now)}
                {active.status === 'Pending' && ` · expires ${formatDateTime(active.expiresAt)}`}
              </div>
            </div>
            {active.status === 'Pending' && (
              <button className="btn-secondary" onClick={() => handleCancelCommand(server, active.id)}>Cancel</button>
            )}
          </div>
        )}

        <div className="server-actions server-remote-actions">
          <button className="btn-delete" disabled={!reboot.allowed} onClick={() => openCommand(server, 'Reboot')}>Reboot PC</button>
          {showRestartApps && (
            <button className="btn-secondary" disabled={!restartApps.allowed} onClick={() => openCommand(server, 'RestartApps')}>Restart apps</button>
          )}
        </div>
        {reason && !active && <div className="server-subtext">{reason}</div>}
        <div className="server-subtext">
          Reboots in the last 24 hours: {server.rebootsLast24Hours} of {server.maxRebootsPerDay}
        </div>

        {server.commands.length > 0 && (
          <table className="server-mini-table">
            <thead>
              <tr><th>Command</th><th>Requested</th><th>Result</th></tr>
            </thead>
            <tbody>
              {server.commands.map(command => (
                <tr key={command.id}>
                  <td>{commandLabel(command.type)}</td>
                  <td>{formatDateTime(command.requestedAt)}<div className="server-subtext">{command.requestedBy}</div></td>
                  <td className={command.status === 'Failed' || command.status === 'Rejected' ? 'server-text-degraded' : undefined}>
                    {commandStatusLabel(command)}
                    {command.resultMessage && <div className="server-subtext">{command.resultMessage}</div>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>
    );
  };

  const counts = useMemo(() => countByStatus(servers), [servers]);
  const visibleServers = useMemo(() => sortServers(filterServers(servers, filter)), [servers, filter]);
  const selected = servers.find(s => s.beaconID === selectedId) ?? null;

  const setSelectedId = (beaconId: number | null) => {
    setSearchParams(beaconId === null ? {} : { beacon: String(beaconId) }, { replace: true });
  };

  const openEdit = (server: ServerHealth) => {
    setEditing(server);
    setForm({
      sdrSharp: toField(server.expectedSdrSharpCount),
      sdrSlices: toField(server.expectedSdrSliceCount),
      softEot: toField(server.expectedSoftEotCount),
      softDpu: toField(server.expectedSoftDpuCount),
    });
    setFormError(undefined);
  };

  const handleSave = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editing) return;

    const sdrSharp = parseExpectedCount(form.sdrSharp);
    const softEot = parseExpectedCount(form.softEot);
    const softDpu = parseExpectedCount(form.softDpu);
    const sdrSlices = parseExpectedCount(form.sdrSlices);
    if (sdrSharp === undefined || softEot === undefined || softDpu === undefined || sdrSlices === undefined) {
      setFormError('Enter a whole number from 0 to 20, or leave the field blank to skip the check.');
      return;
    }

    const response = await updateServerExpectations(editing.beaconID, {
      expectedSdrSharpCount: sdrSharp,
      expectedSoftEotCount: softEot,
      expectedSoftDpuCount: softDpu,
      expectedSdrSliceCount: sdrSlices,
    });
    if (response.errors.length > 0) {
      setFormError(response.errors.join(', '));
      return;
    }

    setEditing(null);
    await loadServers();
  };

  if (loading) return <div className="admin-loading">Loading...</div>;

  return (
    <div className="admin-page admin-page--wide admin-server-health">
      <AdminPageHeader
        title="Server Health"
        description="Whether each beacon PC is checking in and running SDR#, SoftEOT and SoftDPU. Refreshes every minute and whenever a status changes."
        actions={(
          <>
            {notificationPermission === 'default' && (
              <button className="btn-secondary" onClick={enableDesktopAlerts}>Turn on desktop alerts</button>
            )}
            {notificationPermission === 'denied' && (
              <span className="server-subtext">Desktop alerts are blocked in this browser's site settings.</span>
            )}
            <button className="btn-secondary" onClick={loadServers}>Refresh</button>
          </>
        )}
      />

      <div className="server-health-summary">
        {(['Online', 'Degraded', 'Offline'] as ServerStatus[]).map(status => (
          <button
            key={status}
            type="button"
            className={`server-health-card ${STATUS_CLASS[status]} ${filter === status ? 'active' : ''}`}
            onClick={() => setFilter(filter === status ? 'all' : status)}
            aria-pressed={filter === status}
          >
            <span className="server-health-card-count">{counts[status]}</span>
            <span className="server-health-card-label">{status}</span>
          </button>
        ))}
        <label className="server-health-filter">
          <span>Show</span>
          <select value={filter} onChange={e => setFilter(e.target.value as ServerHealthFilter)}>
            <option value="all">All servers</option>
            <option value="problems">Problems only</option>
            <option value="Offline">Offline</option>
            <option value="Degraded">Degraded</option>
            <option value="Online">Online</option>
          </select>
        </label>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="admin-table-container">
        <table className="admin-table server-health-table">
          <thead>
            <tr>
              <th>Status</th>
              <th>Server</th>
              <th>Last check-in</th>
              <th>Problems</th>
              <th>Programs</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {visibleServers.length === 0 && (
              <tr>
                <td colSpan={6} className="server-health-empty">No servers match this filter.</td>
              </tr>
            )}
            {visibleServers.map(server => (
              <tr key={server.beaconID} className={server.alertsMuted ? 'server-row-muted' : undefined}>
                <td><StatusChip status={server.status} /></td>
                <td>
                  <div className="server-name">{server.beaconName}</div>
                  <div className="server-subtext">
                    {server.diagnostics?.hostname ?? (server.hasDiagnostics ? '' : 'No diagnostics reported')}
                    {server.diagnostics?.clientVersion ? ` · v${server.diagnostics.clientVersion}` : ''}
                  </div>
                  {server.alertsMuted && (
                    <div className="server-muted-note" title={server.alertsMuteNote ?? undefined}>
                      🔕 {describeMute(server)}{server.alertsMuteNote ? `: ${server.alertsMuteNote}` : ''}
                    </div>
                  )}
                  {(() => {
                    const command = activeCommand(server);
                    return command && (
                      <div className="server-command-note">
                        {commandLabel(command.type)}: {commandStatusLabel(command).toLowerCase()} ({command.requestedBy})
                      </div>
                    );
                  })()}
                </td>
                <td className={server.status === 'Offline' ? 'server-text-offline' : undefined}>
                  {formatTimeAgo(server.lastCheckIn, now)}
                </td>
                <td>
                  {server.reasons.length === 0
                    ? <span className="server-subtext">—</span>
                    : server.reasons.map(reason => (
                      <div key={reason} className={`server-reason ${STATUS_CLASS[server.status]}`}>{reason}</div>
                    ))}
                </td>
                <td>
                  <div className="server-programs">
                    {server.programs.filter(isProgramInSummary).map(program => (
                      <span key={program.name} className={isProgramShort(program) || program.notResponding > 0 ? 'server-text-degraded' : undefined}>
                        {program.name} {formatProgramCount(program)}
                      </span>
                    ))}
                  </div>
                </td>
                <td>
                  <div className="server-actions">
                    <button className="btn-primary" onClick={() => setSelectedId(server.beaconID)}>Details</button>
                    <button className="btn-secondary" onClick={() => openEdit(server)}>Expected programs</button>
                    {renderMuteButton(server)}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {selected && (
        <div className="server-drawer-overlay" onClick={() => setSelectedId(null)}>
          <aside className="server-drawer" onClick={e => e.stopPropagation()} aria-label={`${selected.beaconName} details`}>
            <div className="server-drawer-header">
              <h2>{selected.beaconName}</h2>
              <button className="server-drawer-close" onClick={() => setSelectedId(null)} aria-label="Close details">✕</button>
            </div>
            <div className="server-drawer-meta">
              <StatusChip status={selected.status} />
              {selected.statusSince && <span>{selected.status} for {formatUptime(selected.statusSince, now)}</span>}
              <span>Last check-in {formatTimeAgo(selected.lastCheckIn, now)}</span>
              {selected.diagnostics && (
                <>
                  <span>{selected.diagnostics.hostname}</span>
                  <span>Up {formatUptime(selected.diagnostics.lastBootAt, now)}</span>
                  <span>ConsoleApp {selected.diagnostics.clientVersion ?? 'unknown'}</span>
                </>
              )}
            </div>
            {selected.subdivisions.length > 0 && (
              <div className="server-subtext">Serves {selected.subdivisions.join(', ')}</div>
            )}

            <section className="server-drawer-section">
              <h3>Alerts</h3>
              <div className="server-drawer-alerts">
                <div>
                  {selected.alertsMuted ? (
                    <>
                      <div className="server-muted-note">🔕 {describeMute(selected)}</div>
                      {selected.alertsMuteNote && <div className="server-subtext">{selected.alertsMuteNote}</div>}
                    </>
                  ) : (
                    <div className="server-subtext">Support users get a pop-up when this server goes offline, becomes degraded or recovers.</div>
                  )}
                </div>
                {renderMuteButton(selected)}
              </div>
            </section>

            {renderRemoteSection(selected)}

            {selected.reasons.length > 0 && (
              <section className="server-drawer-section">
                <h3>Problems</h3>
                {selected.reasons.map(reason => (
                  <div key={reason} className={`server-reason ${STATUS_CLASS[selected.status]}`}>{reason}</div>
                ))}
              </section>
            )}

            <section className="server-drawer-section">
              <h3>
                Programs
                <button className="server-link-button" onClick={() => openEdit(selected)}>Edit expected</button>
              </h3>
              <table className="server-mini-table">
                <thead>
                  <tr><th>Program</th><th>Expected</th><th>Running</th><th>Not responding</th></tr>
                </thead>
                <tbody>
                  {selected.programs.map(program => (
                    <tr key={program.name}>
                      <td>{program.name}</td>
                      <td>{program.expected ?? 'Not checked'}</td>
                      <td className={isProgramShort(program) ? 'server-text-degraded' : undefined}>{program.running ?? '—'}</td>
                      <td className={program.notResponding > 0 ? 'server-text-degraded' : undefined}>{program.running === null ? '—' : program.notResponding}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </section>

            {selected.diagnostics ? (
              <>
                <section className="server-drawer-section">
                  <h3>Log files</h3>
                  <table className="server-mini-table">
                    <thead>
                      <tr><th>Log</th><th>Last written</th></tr>
                    </thead>
                    <tbody>
                      {selected.diagnostics.logs.map(log => (
                        <tr key={log.prefix}>
                          <td>{log.prefix}</td>
                          <td>{log.lastWriteAt ? formatTimeAgo(log.lastWriteAt, now) : 'No file'}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </section>

                <section className="server-drawer-section">
                  <h3>Startup tasks</h3>
                  {selected.diagnostics.startupTasks.length === 0 ? (
                    <div className="server-subtext">
                      No startup tasks are monitored. List the task names in MonitoredScheduledTasks in the PC's ConsoleApp settings.
                    </div>
                  ) : (
                    <table className="server-mini-table">
                      <thead>
                        <tr><th>Task</th><th>Last run</th><th>Result</th></tr>
                      </thead>
                      <tbody>
                        {selected.diagnostics.startupTasks.map(task => (
                          <tr key={task.name}>
                            <td>{task.name}</td>
                            <td>{task.found ? (task.lastRunAt ? new Date(task.lastRunAt).toLocaleString() : 'Never') : 'Not found'}</td>
                            <td className={task.found && task.lastResult !== 0 ? 'server-text-degraded' : undefined}>
                              {task.found ? formatTaskResult(task.lastResult) : '—'}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  )}
                </section>

                <section className="server-drawer-section">
                  <h3>Connection</h3>
                  <div className="server-subtext">
                    Telemetry last sent {formatTimeAgo(selected.diagnostics.lastTelemetrySentAt, now)}
                    {' · '}{selected.diagnostics.telemetrySendFailuresLastHour} send failures in the last hour
                    {' · '}Checks in every {selected.diagnostics.healthPingIntervalMinutes} min (offline after {selected.offlineThresholdMinutes} min)
                    {' · '}Disk free {formatGigabytes(selected.diagnostics.logDriveFreeBytes)}
                  </div>
                  <div className="server-subtext">{selected.diagnostics.osVersion}</div>
                </section>
              </>
            ) : (
              <section className="server-drawer-section">
                <div className="server-subtext">
                  This PC has not sent a diagnostics report. Update its ConsoleApp to see program, log and startup task details.
                </div>
              </section>
            )}
          </aside>
        </div>
      )}

      {editing && (
        <div className="modal-overlay" onClick={() => setEditing(null)}>
          <div className="modal-content" onClick={e => e.stopPropagation()}>
            <h2>Expected programs: {editing.beaconName}</h2>
            <p className="server-subtext">
              The server is marked Degraded when fewer copies than this are running. Leave a field blank to skip that check.
            </p>
            <form onSubmit={handleSave}>
              {formError && <div className="error-message">{formError}</div>}
              {([
                ['sdrSharp', 'SDR#'],
                ['softEot', 'SoftEOT'],
                ['softDpu', 'SoftDPU'],
                ['sdrSlices', 'SDR# slices'],
              ] as [keyof ExpectationsForm, string][]).map(([key, label]) => (
                <div className="form-group" key={key}>
                  <label htmlFor={`expected-${key}`}>{label}</label>
                  <input
                    id={`expected-${key}`}
                    type="number"
                    min={0}
                    max={20}
                    value={form[key]}
                    onChange={e => setForm({ ...form, [key]: e.target.value })}
                    placeholder="Not checked"
                  />
                  {key === 'sdrSlices' && (
                    <div className="server-subtext">
                      Sharp Slicer windows in SDR#, usually one per SoftEOT and SoftDPU. Leave blank if this PC does not use slice mode.
                    </div>
                  )}
                </div>
              ))}
              <div className="form-actions">
                <button type="button" className="btn-secondary" onClick={() => setEditing(null)}>Cancel</button>
                <button type="submit" className="btn-primary">Save</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {muting && (
        <div className="modal-overlay" onClick={() => setMuting(null)}>
          <div className="modal-content" onClick={e => e.stopPropagation()}>
            <h2>Mute alerts: {muting.beaconName}</h2>
            <p className="server-subtext">
              No pop-ups for this server until the mute ends. It stays on this page with its current status.
            </p>
            <form onSubmit={handleMute}>
              {muteError && <div className="error-message">{muteError}</div>}
              <fieldset className="server-mute-durations">
                <legend>Mute for</legend>
                {MUTE_DURATIONS.map(option => (
                  <label key={option.value}>
                    <input
                      type="radio"
                      name="mute-duration"
                      value={option.value}
                      checked={muteDuration === option.value}
                      onChange={() => setMuteDuration(option.value)}
                    />
                    {option.label}
                  </label>
                ))}
              </fieldset>
              <div className="form-group">
                <label htmlFor="mute-note">Note (optional)</label>
                <textarea
                  id="mute-note"
                  rows={3}
                  maxLength={500}
                  value={muteNote}
                  onChange={e => setMuteNote(e.target.value)}
                  placeholder="For example: Antenna down until the spring thaw"
                />
              </div>
              <div className="form-actions">
                <button type="button" className="btn-secondary" onClick={() => setMuting(null)}>Cancel</button>
                <button type="submit" className="btn-primary">Mute alerts</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {commandConfirm && (
        <div className="modal-overlay" onClick={() => setCommandConfirm(null)}>
          <div className="modal-content" onClick={e => e.stopPropagation()}>
            <h2>{commandConfirm.type === 'Reboot' ? 'Reboot' : 'Restart apps on'} {commandConfirm.server.beaconName}?</h2>
            {commandConfirm.type === 'Reboot' ? (
              <>
                <p className="server-subtext">
                  The PC picks this up at its next check-in, within {commandConfirm.server.diagnostics?.healthPingIntervalMinutes ?? 2} minutes,
                  and restarts Windows 60 seconds later. Its startup tasks then open SDR#, SoftEOT, SoftDPU and the ConsoleApp again.
                  The server shows Offline while it restarts. If the PC does not pick it up within 10 minutes, it is not carried out.
                </p>
                <p className="server-subtext">
                  Reboots in the last 24 hours: {commandConfirm.server.rebootsLast24Hours} of {commandConfirm.server.maxRebootsPerDay}.
                </p>
              </>
            ) : (
              <p className="server-subtext">
                The PC runs its restart apps command at its next check-in. It closes and reopens SDR#, SoftEOT and SoftDPU without restarting Windows.
              </p>
            )}
            <form onSubmit={handleCommand}>
              {commandError && <div className="error-message">{commandError}</div>}
              {commandConfirm.type === 'Reboot' && (
                <div className="form-group">
                  <label htmlFor="confirm-server-name">Type the server name, {commandConfirm.server.beaconName}, to confirm</label>
                  <input
                    id="confirm-server-name"
                    autoComplete="off"
                    value={confirmName}
                    onChange={e => setConfirmName(e.target.value)}
                  />
                </div>
              )}
              <div className="form-actions">
                <button type="button" className="btn-secondary" onClick={() => setCommandConfirm(null)}>Cancel</button>
                <button
                  type="submit"
                  className={commandConfirm.type === 'Reboot' ? 'btn-delete' : 'btn-primary'}
                  disabled={busy || (commandConfirm.type === 'Reboot'
                    && confirmName.trim().toLowerCase() !== commandConfirm.server.beaconName.trim().toLowerCase())}
                >
                  {commandConfirm.type === 'Reboot' ? 'Reboot PC' : 'Restart apps'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {keyDialog && (
        <div className="modal-overlay" onClick={() => keyDialog.stage !== 'show' && setKeyDialog(null)}>
          <div className="modal-content" onClick={e => e.stopPropagation()}>
            {keyError && <div className="error-message">{keyError}</div>}
            {keyDialog.stage === 'confirm' && (
              <>
                <h2>{keyDialog.server.deviceKeySetAt ? 'Replace' : 'Create'} device key: {keyDialog.server.beaconName}</h2>
                <p className="server-subtext">
                  The PC sends this key with every check-in. Once it exists, this site refuses check-ins without it, so the server shows
                  Offline until the key is entered on the PC. Have access to the PC ready before you continue.
                </p>
                {keyDialog.server.deviceKeySetAt && (
                  <p className="server-subtext">The current key stops working straight away.</p>
                )}
                <div className="form-actions">
                  <button type="button" className="btn-secondary" onClick={() => setKeyDialog(null)}>Cancel</button>
                  <button type="button" className="btn-primary" disabled={busy} onClick={handleCreateKey}>
                    {keyDialog.server.deviceKeySetAt ? 'Replace key' : 'Create key'}
                  </button>
                </div>
              </>
            )}
            {keyDialog.stage === 'show' && keyDialog.key && (
              <>
                <h2>Device key for {keyDialog.server.beaconName}</h2>
                <p className="server-subtext">This key is shown only once. If it is lost, replace it.</p>
                <div className="server-device-key">
                  <input readOnly value={keyDialog.key} aria-label="Device key" onFocus={e => e.target.select()} />
                  <button type="button" className="btn-secondary" onClick={copyKey}>{keyCopied ? 'Copied' : 'Copy'}</button>
                </div>
                <ol className="server-key-steps">
                  <li>On the PC, click the RugbyJunctionTrainTracker console window and press Ctrl+U.</li>
                  <li>Press Enter to keep the log folder and beacon IDs.</li>
                  <li>When asked for the device key for the subscriber that sends to {apiUrl}, paste this key.</li>
                  <li>Answer y to “Allow reboot and restart apps from the Server Health page?”.</li>
                </ol>
                <p className="server-subtext">The PC shows here as allowing remote commands after its next check-in.</p>
                <div className="form-actions">
                  <button type="button" className="btn-primary" onClick={() => setKeyDialog(null)}>Done</button>
                </div>
              </>
            )}
            {keyDialog.stage === 'remove' && (
              <>
                <h2>Remove device key: {keyDialog.server.beaconName}?</h2>
                <p className="server-subtext">
                  This site accepts check-ins from the PC without a key again, and the PC can no longer be rebooted from here.
                </p>
                <div className="form-actions">
                  <button type="button" className="btn-secondary" onClick={() => setKeyDialog(null)}>Cancel</button>
                  <button type="button" className="btn-delete" disabled={busy} onClick={handleRemoveKey}>Remove key</button>
                </div>
              </>
            )}
          </div>
        </div>
      )}
    </div>
  );
};

export default AdminServerHealth;
