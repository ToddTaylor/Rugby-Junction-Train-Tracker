import { useCallback, useEffect, useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import type { ServerHealth, ServerStatus } from '../types/ServerHealth';
import { getServerHealth, updateServerExpectations } from '../api/serverHealth';
import AdminPageHeader from '../components/admin/AdminPageHeader';
import {
  ServerHealthFilter,
  countByStatus,
  filterServers,
  formatGigabytes,
  formatProgramCount,
  formatTaskResult,
  formatTimeAgo,
  formatUptime,
  isProgramShort,
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

type ExpectationsForm = { sdrSharp: string; softEot: string; softDpu: string };

const toField = (value: number | null) => (value === null ? '' : String(value));

const AdminServerHealth = () => {
  const [servers, setServers] = useState<ServerHealth[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [filter, setFilter] = useState<ServerHealthFilter>('all');
  const [searchParams, setSearchParams] = useSearchParams();
  // ?beacon=<id> opens that server's details, so other pages can link straight to it.
  const selectedId = Number(searchParams.get('beacon')) || null;
  const [editing, setEditing] = useState<ServerHealth | null>(null);
  const [form, setForm] = useState<ExpectationsForm>({ sdrSharp: '', softEot: '', softDpu: '' });
  const [formError, setFormError] = useState<string>();
  const [now, setNow] = useState(() => new Date());

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
    if (sdrSharp === undefined || softEot === undefined || softDpu === undefined) {
      setFormError('Enter a whole number from 0 to 20, or leave the field blank to skip the check.');
      return;
    }

    const response = await updateServerExpectations(editing.beaconID, {
      expectedSdrSharpCount: sdrSharp,
      expectedSoftEotCount: softEot,
      expectedSoftDpuCount: softDpu,
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
        description="Whether each beacon PC is checking in and running SDR#, SoftEOT and SoftDPU. Refreshes every minute."
        actions={<button className="btn-secondary" onClick={loadServers}>Refresh</button>}
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
              <tr key={server.beaconID}>
                <td><StatusChip status={server.status} /></td>
                <td>
                  <div className="server-name">{server.beaconName}</div>
                  <div className="server-subtext">
                    {server.diagnostics?.hostname ?? (server.hasDiagnostics ? '' : 'No diagnostics reported')}
                    {server.diagnostics?.clientVersion ? ` · v${server.diagnostics.clientVersion}` : ''}
                  </div>
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
                    {server.programs.map(program => (
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
    </div>
  );
};

export default AdminServerHealth;
