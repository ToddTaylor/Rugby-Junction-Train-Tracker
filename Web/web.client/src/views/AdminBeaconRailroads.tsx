import { useState, useEffect, useMemo, Fragment } from 'react';
import { AdminBeaconRailroad, CreateBeaconRailroad, UpdateBeaconRailroad, Direction } from '../types/AdminBeaconRailroad';
import { AdminBeacon } from '../types/AdminBeacon';
import { Subdivision } from '../types/Subdivision';
import { getBeaconRailroads, createBeaconRailroad, updateBeaconRailroad, deleteBeaconRailroad } from '../api/beaconRailroads';
import { getBeacons } from '../api/beacons';
import { getSubdivisions } from '../api/subdivisions';
import './AdminBeaconRailroads.css';
import './AdminSkin.css';
import TextField from '@mui/material/TextField';
import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import ClearIcon from '@mui/icons-material/Clear';
import AdminPageHeader from '../components/admin/AdminPageHeader';
import { adminClearButtonSx } from '../components/admin/adminSx';
import { useAuth } from '../hooks/useAuth';
import { parseSessionRoles } from '../utils/roles';
import { BEACON_ONLINE_COLOR, BEACON_OFFLINE_COLOR } from '../constants/beaconColors';
import { compareCoordinates, formatSeparation } from '../utils/beaconCoordinates';

type SortField = 'beaconName' | 'subdivisionName' | 'railroadName' | 'milepost';
type SortDirection = 'asc' | 'desc' | null;

const DIRECTION_OPTIONS: Direction[] = ['All', 'NorthSouth', 'EastWest', 'NortheastSouthwest', 'NorthwestSoutheast'];

/**
 * Validates the optional telemetryStaleHoursOverride value.
 * Returns an error string if invalid, or null if valid (including when the value is null/undefined).
 */
export function validateTelemetryStaleHoursOverride(value: number | null | undefined): string | null {
  if (value === null || value === undefined) return null;
  if (!Number.isInteger(value) || value <= 0) {
    return 'Telemetry stale hours override must be a whole integer greater than zero';
  }
  return null;
}

/**
 * Determines whether the current user may edit the Offline Note field for a beacon railroad.
 * Admins can always edit; Custodians only for beacon railroads on their own assigned subdivision.
 */
export function canEditOfflineNote(
  isAdmin: boolean,
  isCustodian: boolean,
  subdivisionCustodianId: number | null | undefined,
  currentUserId: number | null | undefined
): boolean {
  if (isAdmin) return true;
  return isCustodian && subdivisionCustodianId != null && subdivisionCustodianId === currentUserId;
}

export interface BeaconGroup {
  beaconID: number;
  beaconName: string;
  rows: AdminBeaconRailroad[];
}

/**
 * Groups beacon railroad rows by beacon, preserving the incoming order of both the groups
 * and the rows within each group.
 *
 * A beacon has one row per subdivision, each with its own milepost. At a junction such as
 * Junction City - where one railroad crosses its own tracks - those rows carry very different
 * mileposts, so showing them together is what makes the per-subdivision model legible.
 */
export function groupByBeacon(rows: AdminBeaconRailroad[]): BeaconGroup[] {
  const groups: BeaconGroup[] = [];
  const byBeaconID = new Map<number, BeaconGroup>();

  for (const row of rows) {
    let group = byBeaconID.get(row.beaconID);

    if (!group) {
      group = { beaconID: row.beaconID, beaconName: row.beaconName, rows: [] };
      byBeaconID.set(row.beaconID, group);
      groups.push(group);
    }

    group.rows.push(row);
  }

  return groups;
}

/**
 * Subdivisions still assignable to a beacon: all except those it already has a row for.
 * The composite key (beaconID, subdivisionID) makes re-picking an assigned subdivision a
 * guaranteed server error, so it is filtered out of the dropdown instead.
 *
 * When editing an existing row, that row's own subdivision stays in the list so the select
 * can still display it.
 */
export function availableSubdivisions(
  all: Subdivision[],
  existingRows: AdminBeaconRailroad[],
  keepSubdivisionID?: number
): Subdivision[] {
  const taken = new Set(
    existingRows
      .map(row => row.subdivisionID)
      .filter(id => id !== keepSubdivisionID)
  );

  return all.filter(subdivision => !taken.has(subdivision.id));
}

export interface CoordinateNotice {
  severity: 'info' | 'warning';
  message: string;
}

/**
 * Describes how the coordinates being entered relate to the beacon's other subdivision rows.
 *
 * Returns null when there is nothing to compare against, meaning the beacon's first row or an
 * incomplete entry. A warning marks a separation too small to be a second track and too large
 * to be the same point, which is the signature of a mistyped digit.
 */
export function describeCoordinateRelation(
  candidate: { latitude: number; longitude: number },
  siblings: AdminBeaconRailroad[]
): CoordinateNotice | null {
  if (siblings.length === 0) return null;
  if (!Number.isFinite(candidate.latitude) || !Number.isFinite(candidate.longitude)) return null;
  if (candidate.latitude === 0 && candidate.longitude === 0) return null;

  const nearest = siblings
    .map(sibling => ({ sibling, comparison: compareCoordinates(candidate, sibling) }))
    .sort((a, b) => a.comparison.feet - b.comparison.feet)[0];

  const name = `${nearest.sibling.railroadName} ${nearest.sibling.subdivisionName}`;

  if (nearest.comparison.relation === 'same') {
    return {
      severity: 'info',
      message: `Same location as ${name}. This is a junction: one point, one milepost per subdivision.`
    };
  }

  if (nearest.comparison.relation === 'nearby') {
    return {
      severity: 'warning',
      message: `${formatSeparation(nearest.comparison.feet)} from ${name}. That is too close to be a separate track. Check for a mistyped digit, or use "same location as" to match exactly.`
    };
  }

  return {
    severity: 'info',
    message: `${formatSeparation(nearest.comparison.feet)} from ${name}. Separate track.`
  };
}

/**
 * Validates a milepost. Required, finite, and greater than zero.
 * A cleared number input yields NaN, which serializes to null and would otherwise be stored.
 */
export function validateMilepost(value: number | null | undefined): string | null {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return 'Milepost is required and must be a number greater than zero';
  }

  if (!Number.isFinite(value) || value <= 0) {
    return 'Milepost is required and must be a number greater than zero';
  }

  return null;
}

const AdminBeaconRailroads = () => {
  const [beaconRailroads, setBeaconRailroads] = useState<AdminBeaconRailroad[]>([]);
  const [beacons, setBeacons] = useState<AdminBeacon[]>([]);
  const [subdivisions, setSubdivisions] = useState<Subdivision[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [showModal, setShowModal] = useState(false);
  const [editingBeaconRailroad, setEditingBeaconRailroad] = useState<AdminBeaconRailroad | null>(null);
  const [lockedBeaconID, setLockedBeaconID] = useState<number | null>(null);
  const [formData, setFormData] = useState<CreateBeaconRailroad>({
    beaconID: 0,
    subdivisionID: 0,
    latitude: 0,
    longitude: 0,
    milepost: 0,
    multipleTracks: false,
    online: true,
    direction: 'All',
    telemetryStaleHoursOverride: null,
    offlineNote: null
  });
  // Subdivision ID of the row this entry mirrors coordinates from, or null when the coordinates
  // are entered directly. Mirroring keeps a junction's rows exactly equal rather than equal only
  // if both were typed correctly.
  const [mirrorSubdivisionID, setMirrorSubdivisionID] = useState<number | null>(null);
  const [searchTerm, setSearchTerm] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(10);
  const [sortField, setSortField] = useState<SortField>('beaconName');
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc');

  const { session } = useAuth();
  const { isAdmin, isCustodian } = parseSessionRoles(session?.roles);
  const currentUserId = session?.userId ?? null;
  const editingSubdivision = editingBeaconRailroad
    ? subdivisions.find(s => s.id === editingBeaconRailroad.subdivisionID)
    : undefined;
  const canEditNote = canEditOfflineNote(isAdmin, isCustodian, editingSubdivision?.custodianId, currentUserId);

  // Rows already assigned to the beacon being added to, used to fill the callout and to keep
  // already-taken subdivisions out of the dropdown.
  const lockedBeaconExistingRows = lockedBeaconID !== null
    ? beaconRailroads.filter(br => br.beaconID === lockedBeaconID)
    : [];
  const lockedBeaconName = lockedBeaconExistingRows[0]?.beaconName
    ?? beacons.find(b => b.id === lockedBeaconID)?.name
    ?? '';

  // A beacon can hold only one row per subdivision (composite key), so filter out the ones
  // it already has rather than letting the server reject a duplicate.
  const selectableSubdivisions = editingBeaconRailroad
    ? subdivisions
    : availableSubdivisions(
        subdivisions,
        lockedBeaconID !== null
          ? lockedBeaconExistingRows
          : beaconRailroads.filter(br => br.beaconID === formData.beaconID)
      );
  const canEditStructuralFields = isAdmin || !editingBeaconRailroad;

  // The beacon's other rows, whichever way the modal was opened. Editing excludes the row being
  // edited, since a row is not its own sibling.
  const siblingRows = beaconRailroads.filter(br =>
    br.beaconID === formData.beaconID &&
    !(editingBeaconRailroad
      && br.beaconID === editingBeaconRailroad.beaconID
      && br.subdivisionID === editingBeaconRailroad.subdivisionID)
  );

  const mirrorSource = mirrorSubdivisionID !== null
    ? siblingRows.find(row => row.subdivisionID === mirrorSubdivisionID)
    : undefined;

  const coordinateNotice = mirrorSource
    ? null
    : describeCoordinateRelation(
        { latitude: formData.latitude, longitude: formData.longitude },
        siblingRows
      );

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);

    const [brResponse, beaconsResponse, subdivisionsResponse] = await Promise.all([
      getBeaconRailroads(),
      getBeacons(),
      getSubdivisions()
    ]);

    if (brResponse.errors.length > 0) {
      setError(brResponse.errors.join(', '));
    } else if (brResponse.data) {
      setBeaconRailroads(brResponse.data);
    }

    if (beaconsResponse.data) {
      setBeacons(beaconsResponse.data);
    }

    if (subdivisionsResponse.data) {
      setSubdivisions(subdivisionsResponse.data);
    }

    setLoading(false);
  };

  const handleSort = (field: SortField) => {
    if (sortField === field) {
      setSortDirection(sortDirection === 'asc' ? 'desc' : sortDirection === 'desc' ? null : 'asc');
    } else {
      setSortField(field);
      setSortDirection('asc');
    }
  };

  const sortedBeaconRailroads = useMemo(() => {
    let sorted = [...beaconRailroads];

    if (searchTerm) {
      const lower = searchTerm.toLowerCase();
      sorted = sorted.filter(br =>
        br.beaconName.toLowerCase().includes(lower) ||
        br.subdivisionName.toLowerCase().includes(lower) ||
        br.railroadName.toLowerCase().includes(lower)
      );
    }

    if (sortDirection) {
      sorted.sort((a, b) => {
        let aVal: string | number = a[sortField];
        let bVal: string | number = b[sortField];

        if (typeof aVal === 'string') {
          aVal = aVal.toLowerCase();
          bVal = (bVal as string).toLowerCase();
        }

        if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1;
        if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1;
        return 0;
      });
    }

    return sorted;
  }, [beaconRailroads, searchTerm, sortField, sortDirection]);

  const getSortIcon = (field: SortField) => {
    const icon = sortField !== field ? '⇅' : sortDirection === 'asc' ? '⬆' : '⬇';
    return <span style={{ fontSize: '1.2em', marginLeft: '0.3em' }}>{icon}</span>;
  };

  // Group by beacon so a junction's per-subdivision mileposts stay together.
  const beaconGroups = useMemo(() => groupByBeacon(sortedBeaconRailroads), [sortedBeaconRailroads]);

  // Paginate by beacon rather than by row, so a beacon's subdivisions are never split
  // across a page boundary.
  const paginatedBeaconGroups = useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage;
    return beaconGroups.slice(startIndex, startIndex + itemsPerPage);
  }, [beaconGroups, currentPage, itemsPerPage]);

  const totalPages = Math.ceil(beaconGroups.length / itemsPerPage);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchTerm, sortField, sortDirection, itemsPerPage]);

  useEffect(() => {
    if (currentPage > totalPages && totalPages > 0) {
      setCurrentPage(totalPages);
    }
  }, [currentPage, totalPages]);

  /**
   * Points the coordinates at a sibling row, or releases them for direct entry.
   * Copying on selection means the stored values are exactly the sibling's, and releasing
   * leaves them in place as the starting point for an edit.
   */
  const handleMirrorChange = (subdivisionID: number | null) => {
    setMirrorSubdivisionID(subdivisionID);

    if (subdivisionID === null) return;

    const source = siblingRows.find(row => row.subdivisionID === subdivisionID);

    if (source) {
      setFormData(current => ({
        ...current,
        latitude: source.latitude,
        longitude: source.longitude
      }));
    }
  };

  const handleAdd = () => {
    setEditingBeaconRailroad(null);
    setLockedBeaconID(null);
    setMirrorSubdivisionID(null);
    setFormData({
      beaconID: beacons.length > 0 ? beacons[0].id : 0,
      subdivisionID: subdivisions.length > 0 ? subdivisions[0].id : 0,
      latitude: 0,
      longitude: 0,
      milepost: 0,
      multipleTracks: false,
      online: true,
      direction: 'All',
      telemetryStaleHoursOverride: null,
      offlineNote: null
    });
    setError(undefined);
    setShowModal(true);
  };

  const handleEdit = (beaconRailroad: AdminBeaconRailroad) => {
    setEditingBeaconRailroad(beaconRailroad);
    setLockedBeaconID(null);
    setMirrorSubdivisionID(null);
    setFormData({
      beaconID: beaconRailroad.beaconID,
      subdivisionID: beaconRailroad.subdivisionID,
      latitude: beaconRailroad.latitude,
      longitude: beaconRailroad.longitude,
      milepost: beaconRailroad.milepost,
      multipleTracks: beaconRailroad.multipleTracks,
      online: beaconRailroad.online,
      direction: beaconRailroad.direction,
      telemetryStaleHoursOverride: beaconRailroad.telemetryStaleHoursOverride ?? null,
      offlineNote: beaconRailroad.offlineNote ?? null
    });
    setError(undefined);
    setShowModal(true);
  };

  /**
   * Adds another subdivision milepost to a beacon that already has at least one.
   *
   * The beacon is fixed. Coordinates mirror a sibling row by default, which is correct for a
   * junction and is the common case for this action. Releasing the mirror allows the separate
   * coordinates that parallel tracks need.
   */
  const handleAddSubdivisionForBeacon = (group: BeaconGroup) => {
    const sibling = group.rows[0];

    setEditingBeaconRailroad(null);
    setLockedBeaconID(group.beaconID);
    setMirrorSubdivisionID(sibling.subdivisionID);
    setFormData({
      beaconID: group.beaconID,
      subdivisionID: 0,
      latitude: sibling.latitude,
      longitude: sibling.longitude,
      milepost: 0,
      multipleTracks: sibling.multipleTracks,
      online: true,
      direction: 'All',
      telemetryStaleHoursOverride: null,
      offlineNote: null
    });
    setError(undefined);
    setShowModal(true);
  };

  const handleDelete = async (beaconId: number, subdivisionId: number) => {
    if (!confirm('Are you sure you want to delete this beacon railroad?')) return;

    const response = await deleteBeaconRailroad(beaconId, subdivisionId);
    if (response.errors.length > 0) {
      setError(response.errors.join(', '));
    } else {
      setBeaconRailroads(beaconRailroads.filter(br =>
        !(br.beaconID === beaconId && br.subdivisionID === subdivisionId)
      ));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(undefined);

    if (formData.beaconID === 0) {
      setError('Beacon is required');
      return;
    }

    if (formData.subdivisionID === 0) {
      setError('Subdivision is required');
      return;
    }

    if (formData.latitude < -90 || formData.latitude > 90) {
      setError('Latitude must be between -90 and 90');
      return;
    }

    if (formData.longitude < -180 || formData.longitude > 180) {
      setError('Longitude must be between -180 and 180');
      return;
    }

    const milepostError = validateMilepost(formData.milepost);
    if (milepostError) {
      setError(milepostError);
      return;
    }

    const overrideError = validateTelemetryStaleHoursOverride(formData.telemetryStaleHoursOverride);
    if (overrideError) {
      setError(overrideError);
      return;
    }

    if (editingBeaconRailroad) {
      const updateData: UpdateBeaconRailroad = { ...formData };
      const response = await updateBeaconRailroad(
        editingBeaconRailroad.beaconID,
        editingBeaconRailroad.subdivisionID,
        updateData
      );
      if (response.errors.length > 0) {
        setError(response.errors.join(', '));
      } else {
        await loadData();
        setShowModal(false);
      }
    } else {
      const response = await createBeaconRailroad(formData);
      if (response.errors.length > 0) {
        setError(response.errors.join(', '));
      } else if (response.data) {
        setBeaconRailroads([...beaconRailroads, response.data]);
        setShowModal(false);
      }
    }
  };

  const formatDirection = (direction: Direction) => {
    switch (direction) {
      case 'NorthSouth': return 'North-South';
      case 'EastWest': return 'East-West';
      case 'NortheastSouthwest': return 'Northeast-Southwest';
      case 'NorthwestSoutheast': return 'Northwest-Southeast';
      default: return direction;
    }
  };

  if (loading) return <div className="admin-loading">Loading...</div>;

  return (
    <div className="admin-beacon-railroads admin-page admin-page--wide">
      <AdminPageHeader
        title="Beacon Railroads"
        description="Configure beacon to subdivision mappings and directional metadata."
      />

      <div className="admin-controls">
        <div className="search-container">
          <TextField
            label="Filter by Beacon, Railroad, or Subdivision"
            variant="outlined"
            size="small"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            className="admin-input"
            fullWidth
            slotProps={{ inputLabel: { shrink: true } }}
          />
          <Tooltip title="Clear filters">
            <IconButton
              sx={adminClearButtonSx}
              aria-label="clear filters"
              onClick={() => {
                setSearchTerm('');
              }}
            >
              <ClearIcon />
            </IconButton>
          </Tooltip>
        </div>
        <div className="right-controls">
          {isAdmin && <button className="btn-primary" onClick={handleAdd}>Add Beacon Railroad</button>}
        </div>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="admin-table-container">
        <table className="admin-table">
          <thead>
            <tr>
              <th className="sortable" onClick={() => handleSort('beaconName')}>
                Beacon {getSortIcon('beaconName')}
              </th>
              <th className="sortable" onClick={() => handleSort('railroadName')}>
                Railroad {getSortIcon('railroadName')}
              </th>
              <th className="sortable" onClick={() => handleSort('subdivisionName')}>
                Subdivision {getSortIcon('subdivisionName')}
              </th>
              <th className="sortable" onClick={() => handleSort('milepost')}>
                Milepost {getSortIcon('milepost')}
              </th>
              <th>Direction</th>
              <th>Multi-Track</th>
              <th>Status</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {paginatedBeaconGroups.map(group => {
              const assignable = availableSubdivisions(subdivisions, group.rows);
              const multipleSubdivisions = group.rows.length > 1;

              return (
              <Fragment key={group.beaconID}>
                <tr className="group-head">
                  <td colSpan={8}>
                    <div className="group-title">
                      <span className="name">{group.beaconName}</span>
                      <span className={`count${multipleSubdivisions ? ' multi' : ''}`}>
                        {group.rows.length} subdivision{group.rows.length === 1 ? '' : 's'}
                      </span>
                      {isAdmin && (
                        <button
                          type="button"
                          className="btn-addsub"
                          onClick={() => handleAddSubdivisionForBeacon(group)}
                          disabled={assignable.length === 0}
                          title={assignable.length === 0
                            ? 'This beacon already has a milepost on every subdivision'
                            : 'Add a milepost for another subdivision through this beacon'}
                        >
                          + Add subdivision milepost
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
                {group.rows.map(br => (
              <tr key={`${br.beaconID}-${br.subdivisionID}`}>
                <td className={`sub-cell${multipleSubdivisions ? ' rail' : ''}`}></td>
                <td>{br.railroadName}</td>
                <td>{br.subdivisionName}</td>
                <td>{br.milepost.toFixed(1)}</td>
                <td>{formatDirection(br.direction)}</td>
                <td>{br.multipleTracks ? 'Yes' : 'No'}</td>
                <td>
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                    <span style={{
                      display: 'inline-block',
                      width: '10px',
                      height: '10px',
                      borderRadius: '50%',
                      background: br.online ? BEACON_ONLINE_COLOR : BEACON_OFFLINE_COLOR
                    }} />
                    {br.online ? 'Online' : 'Offline'}
                  </span>
                </td>
                <td className="actions-cell">
                  <button className="btn-edit" onClick={() => handleEdit(br)}>Edit</button>
                  {isAdmin && (
                    <button className="btn-delete" onClick={() => handleDelete(br.beaconID, br.subdivisionID)}>Delete</button>
                  )}
                </td>
              </tr>
                ))}
              </Fragment>
              );
            })}
          </tbody>
        </table>
        <div className="admin-table-footer-pager">
          <span>Beacons per page:</span>
          <select
            className="admin-table-footer-select"
            value={itemsPerPage}
            onChange={(e) => {
              setItemsPerPage(Number(e.target.value));
            }}
          >
            <option value={10}>10</option>
            <option value={25}>25</option>
            <option value={50}>50</option>
          </select>
          <span>
            {beaconGroups.length === 0
              ? '0-0 of 0 beacons'
              : `${((currentPage - 1) * itemsPerPage) + 1}-${Math.min(currentPage * itemsPerPage, beaconGroups.length)} of ${beaconGroups.length} beacons (${sortedBeaconRailroads.length} mileposts)`}
          </span>
          <button
            type="button"
            className="admin-table-footer-btn"
            onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
            disabled={currentPage === 1 || beaconGroups.length === 0}
            aria-label="Go to previous page"
          >
            ‹
          </button>
          <button
            type="button"
            className="admin-table-footer-btn"
            onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
            disabled={currentPage === totalPages || beaconGroups.length === 0}
            aria-label="Go to next page"
          >
            ›
          </button>
        </div>
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <h2>
              {editingBeaconRailroad
                ? 'Edit Beacon Railroad'
                : lockedBeaconID !== null
                  ? `Add Milepost — ${lockedBeaconName}`
                  : 'Add Beacon Railroad'}
            </h2>
            <form onSubmit={handleSubmit}>
              {error && <div className="error-message">{error}</div>}

              {lockedBeaconID !== null && lockedBeaconExistingRows.length > 0 && (
                <div className="modal-callout">
                  {lockedBeaconName} already has a milepost on{' '}
                  {lockedBeaconExistingRows
                    .map(row => `${row.railroadName} ${row.subdivisionName} (MP ${row.milepost.toFixed(1)})`)
                    .join(', ')}
                  . Each subdivision through this location carries its own milepost.
                </div>
              )}

              <div className="form-group">
                <label htmlFor="beaconID">Beacon *</label>
                <select
                  id="beaconID"
                  value={formData.beaconID}
                  onChange={(e) => {
                    // The mirror points at a row of the previous beacon, so changing the beacon
                    // releases it rather than leaving it pointing at a row that is no longer a
                    // sibling.
                    setMirrorSubdivisionID(null);
                    setFormData({ ...formData, beaconID: parseInt(e.target.value) });
                  }}
                  disabled={!!editingBeaconRailroad || lockedBeaconID !== null}
                >
                  <option value={0}>Select a beacon</option>
                  {beacons.map(beacon => (
                    <option key={beacon.id} value={beacon.id}>
                      {beacon.name}
                    </option>
                  ))}
                </select>
                {lockedBeaconID !== null && (
                  <span className="form-hint">Fixed for this beacon</span>
                )}
              </div>

              <div className="form-group">
                <label htmlFor="subdivisionID">Subdivision *</label>
                <select
                  id="subdivisionID"
                  value={formData.subdivisionID}
                  onChange={(e) => setFormData({ ...formData, subdivisionID: parseInt(e.target.value) })}
                  disabled={!!editingBeaconRailroad}
                >
                  <option value={0}>Select a subdivision</option>
                  {selectableSubdivisions.map(subdivision => (
                    <option key={subdivision.id} value={subdivision.id}>
                      {subdivision.railroad} - {subdivision.name}
                    </option>
                  ))}
                </select>
              </div>

              {siblingRows.length > 0 && canEditStructuralFields && (
                <div className="form-group">
                  <label htmlFor="mirrorSubdivisionID">Location</label>
                  <select
                    id="mirrorSubdivisionID"
                    value={mirrorSubdivisionID ?? 0}
                    onChange={(e) => {
                      const value = parseInt(e.target.value);
                      handleMirrorChange(value === 0 ? null : value);
                    }}
                  >
                    <option value={0}>Enter coordinates directly</option>
                    {siblingRows.map(sibling => (
                      <option key={sibling.subdivisionID} value={sibling.subdivisionID}>
                        Same point as {sibling.railroadName} {sibling.subdivisionName}
                      </option>
                    ))}
                  </select>
                  <span className="form-hint">
                    A junction is one point shared by every subdivision through it. Parallel tracks
                    are separate points, so each carries its own coordinates.
                  </span>
                </div>
              )}

              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="latitude">Latitude *</label>
                  <input
                    id="latitude"
                    type="number"
                    step="0.000001"
                    value={formData.latitude}
                    onChange={(e) => setFormData({ ...formData, latitude: parseFloat(e.target.value) || 0 })}
                    placeholder="43.294944"
                    disabled={!canEditStructuralFields || mirrorSource !== undefined}
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="longitude">Longitude *</label>
                  <input
                    id="longitude"
                    type="number"
                    step="0.000001"
                    value={formData.longitude}
                    onChange={(e) => setFormData({ ...formData, longitude: parseFloat(e.target.value) || 0 })}
                    placeholder="-88.253118"
                    disabled={!canEditStructuralFields || mirrorSource !== undefined}
                  />
                </div>
              </div>

              {mirrorSource && (
                <div className="coordinate-notice coordinate-notice--info">
                  Matched to {mirrorSource.railroadName} {mirrorSource.subdivisionName}. Change
                  Location to enter coordinates directly.
                </div>
              )}

              {coordinateNotice && (
                <div className={`coordinate-notice coordinate-notice--${coordinateNotice.severity}`}>
                  {coordinateNotice.message}
                </div>
              )}

              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="milepost">Milepost *</label>
                  <input
                    id="milepost"
                    type="number"
                    step="0.1"
                    value={formData.milepost}
                    onChange={(e) => setFormData({ ...formData, milepost: parseFloat(e.target.value) || 0 })}
                    placeholder="123.4"
                    disabled={!canEditStructuralFields}
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="direction">Direction *</label>
                  <select
                    id="direction"
                    value={formData.direction}
                    onChange={(e) => setFormData({ ...formData, direction: e.target.value as Direction })}
                    disabled={!canEditStructuralFields}
                  >
                    {DIRECTION_OPTIONS.map(dir => (
                      <option key={dir} value={dir}>
                        {formatDirection(dir)}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="form-group checkbox-group">
                <label>
                  <input
                    type="checkbox"
                    checked={formData.multipleTracks}
                    onChange={(e) => setFormData({ ...formData, multipleTracks: e.target.checked })}
                    disabled={!canEditStructuralFields}
                  />
                  Multiple Tracks
                </label>
              </div>

              <div className="form-group">
                <label htmlFor="telemetryStaleHoursOverride">Telemetry Stale Hours Override (optional)</label>
                <input
                  id="telemetryStaleHoursOverride"
                  type="number"
                  min="1"
                  step="1"
                  value={formData.telemetryStaleHoursOverride ?? ''}
                  onChange={(e) => {
                    const raw = e.target.value;
                    setFormData({
                      ...formData,
                      telemetryStaleHoursOverride: raw === '' ? null : parseInt(raw, 10)
                    });
                  }}
                  placeholder="Leave blank to use default (6 hours)"
                  disabled={!canEditStructuralFields}
                />
              </div>

              {editingBeaconRailroad && (
                <>
                  <div className="form-group">
                    <label>Status</label>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <span style={{
                        display: 'inline-block',
                        width: '10px',
                        height: '10px',
                        borderRadius: '50%',
                        background: editingBeaconRailroad.online ? BEACON_ONLINE_COLOR : BEACON_OFFLINE_COLOR
                      }} />
                      {editingBeaconRailroad.online ? 'Online' : 'Offline'}
                    </div>
                  </div>

                  <div className="form-group">
                    <label htmlFor="offlineNote">Offline Note</label>
                    <div className="form-help">
                      {editingBeaconRailroad.online
                        ? 'Only available while this beacon railroad is offline.'
                        : 'Explain why this beacon railroad is offline. Cleared automatically once it comes back online.'}
                    </div>
                    <textarea
                      id="offlineNote"
                      value={formData.offlineNote || ''}
                      onChange={(e) => setFormData({ ...formData, offlineNote: e.target.value })}
                      rows={4}
                      disabled={editingBeaconRailroad.online || !canEditNote}
                    />
                  </div>
                </>
              )}

              <div className="form-actions">
                <button type="button" className="btn-secondary" onClick={() => setShowModal(false)}>
                  Cancel
                </button>
                <button type="submit" className="btn-primary">
                  {editingBeaconRailroad ? 'Update' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default AdminBeaconRailroads;
