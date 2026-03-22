import { useState, useEffect, useMemo } from 'react';
import { AdminBeaconRailroad, CreateBeaconRailroad, UpdateBeaconRailroad, Direction } from '../types/AdminBeaconRailroad';
import { AdminBeacon } from '../types/AdminBeacon';
import { Subdivision } from '../types/Subdivision';
import { getBeaconRailroads, createBeaconRailroad, updateBeaconRailroad, deleteBeaconRailroad } from '../api/beaconRailroads';
import { getBeacons } from '../api/beacons';
import { getSubdivisions } from '../api/subdivisions';
import './AdminBeaconRailroads.css';
import TextField from '@mui/material/TextField';
import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import ClearIcon from '@mui/icons-material/Clear';

type SortField = 'beaconName' | 'subdivisionName' | 'railroadName' | 'milepost';
type SortDirection = 'asc' | 'desc' | null;

const DIRECTION_OPTIONS: Direction[] = ['All', 'NorthSouth', 'EastWest', 'NortheastSouthwest', 'NorthwestSoutheast'];

const AdminBeaconRailroads = () => {
  const [beaconRailroads, setBeaconRailroads] = useState<AdminBeaconRailroad[]>([]);
  const [beacons, setBeacons] = useState<AdminBeacon[]>([]);
  const [subdivisions, setSubdivisions] = useState<Subdivision[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [showModal, setShowModal] = useState(false);
  const [editingBeaconRailroad, setEditingBeaconRailroad] = useState<AdminBeaconRailroad | null>(null);
  const [formData, setFormData] = useState<CreateBeaconRailroad>({
    beaconID: 0,
    subdivisionID: 0,
    latitude: 0,
    longitude: 0,
    milepost: 0,
    maxDetectionDistanceMiles: null,
    multipleTracks: false,
    online: true,
    direction: 'All'
  });
  const [searchTerm, setSearchTerm] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [sortField, setSortField] = useState<SortField>('beaconName');
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc');
  const itemsPerPage = 10;

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

  const paginatedBeaconRailroads = useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage;
    return sortedBeaconRailroads.slice(startIndex, startIndex + itemsPerPage);
  }, [sortedBeaconRailroads, currentPage]);

  const totalPages = Math.ceil(sortedBeaconRailroads.length / itemsPerPage);

  const handleAdd = () => {
    setEditingBeaconRailroad(null);
    setFormData({
      beaconID: beacons.length > 0 ? beacons[0].id : 0,
      subdivisionID: subdivisions.length > 0 ? subdivisions[0].id : 0,
      latitude: 0,
      longitude: 0,
      milepost: 0,
      maxDetectionDistanceMiles: null,
      multipleTracks: false,
      online: true,
      direction: 'All'
    });
    setError(undefined);
    setShowModal(true);
  };

  const handleEdit = (beaconRailroad: AdminBeaconRailroad) => {
    setEditingBeaconRailroad(beaconRailroad);
    setFormData({
      beaconID: beaconRailroad.beaconID,
      subdivisionID: beaconRailroad.subdivisionID,
      latitude: beaconRailroad.latitude,
      longitude: beaconRailroad.longitude,
      milepost: beaconRailroad.milepost,
      maxDetectionDistanceMiles: beaconRailroad.maxDetectionDistanceMiles,
      multipleTracks: beaconRailroad.multipleTracks,
      online: beaconRailroad.online,
      direction: beaconRailroad.direction
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

    if (formData.maxDetectionDistanceMiles !== null && formData.maxDetectionDistanceMiles <= 0) {
      setError('Max detection distance must be greater than 0 when provided');
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
    <div className="admin-beacon-railroads">
      <div className="admin-header">
        <h1>Beacon Railroads</h1>
        <button className="btn-primary" onClick={handleAdd}>Add Beacon Railroad</button>
      </div>

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
          />
          <Tooltip title="Clear filters">
            <IconButton
              sx={{ color: '#fff', backgroundColor: '#222', '&:hover': { backgroundColor: '#444' }, height: '40px', width: '40px' }}
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
          {totalPages > 1 && (
            <div className="pagination-container">
              <div className="pagination">
                {Array.from({ length: totalPages }, (_, i) => i + 1).map(page => (
                  <button
                    key={page}
                    className={`btn-page ${currentPage === page ? 'active' : ''}`}
                    onClick={() => setCurrentPage(page)}
                  >
                    {page}
                  </button>
                ))}
              </div>
              <div className="results-info">
                Showing {paginatedBeaconRailroads.length} of {sortedBeaconRailroads.length} beacon railroads
              </div>
            </div>
          )}
        </div>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="admin-table-container">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Beacon ID</th>
              <th className="sortable" onClick={() => handleSort('beaconName')}>
                Beacon {getSortIcon('beaconName')}
              </th>
              <th className="sortable" onClick={() => handleSort('railroadName')}>
                Railroad {getSortIcon('railroadName')}
              </th>
              <th className="sortable" onClick={() => handleSort('subdivisionName')}>
                Subdivision {getSortIcon('subdivisionName')}
              </th>
              <th>Milepost</th>
              <th>Direction</th>
              <th>Multi-Track</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {paginatedBeaconRailroads.map(br => (
              <tr key={`${br.beaconID}-${br.subdivisionID}`}>
                <td>{br.beaconID}</td>
                <td>{br.beaconName}</td>
                <td>{br.railroadName}</td>
                <td>{br.subdivisionName}</td>
                <td>{br.milepost.toFixed(1)}</td>
                <td>{formatDirection(br.direction)}</td>
                <td>{br.multipleTracks ? 'Yes' : 'No'}</td>
                <td className="actions-cell">
                  <button className="btn-edit" onClick={() => handleEdit(br)}>Edit</button>
                  <button className="btn-delete" onClick={() => handleDelete(br.beaconID, br.subdivisionID)}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <h2>{editingBeaconRailroad ? 'Edit Beacon Railroad' : 'Add Beacon Railroad'}</h2>
            <form onSubmit={handleSubmit}>
              {error && <div className="error-message">{error}</div>}

              <div className="form-group">
                <label htmlFor="beaconID">Beacon *</label>
                <select
                  id="beaconID"
                  value={formData.beaconID}
                  onChange={(e) => setFormData({ ...formData, beaconID: parseInt(e.target.value) })}
                  disabled={!!editingBeaconRailroad}
                >
                  <option value={0}>Select a beacon</option>
                  {beacons.map(beacon => (
                    <option key={beacon.id} value={beacon.id}>
                      {beacon.name}
                    </option>
                  ))}
                </select>
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
                  {subdivisions.map(subdivision => (
                    <option key={subdivision.id} value={subdivision.id}>
                      {subdivision.railroad} - {subdivision.name}
                    </option>
                  ))}
                </select>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="latitude">Latitude *</label>
                  <input
                    id="latitude"
                    type="number"
                    step="0.000001"
                    value={formData.latitude}
                    onChange={(e) => setFormData({ ...formData, latitude: parseFloat(e.target.value) })}
                    placeholder="43.294944"
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="longitude">Longitude *</label>
                  <input
                    id="longitude"
                    type="number"
                    step="0.000001"
                    value={formData.longitude}
                    onChange={(e) => setFormData({ ...formData, longitude: parseFloat(e.target.value) })}
                    placeholder="-88.253118"
                  />
                </div>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="milepost">Milepost *</label>
                  <input
                    id="milepost"
                    type="number"
                    step="0.1"
                    value={formData.milepost}
                    onChange={(e) => setFormData({ ...formData, milepost: parseFloat(e.target.value) })}
                    placeholder="123.4"
                  />
                </div>

                <div className="form-group">
                  <label htmlFor="maxDetectionDistanceMiles">Max Detection Distance (mi)</label>
                  <input
                    id="maxDetectionDistanceMiles"
                    type="number"
                    step="0.1"
                    min="0"
                    value={formData.maxDetectionDistanceMiles ?? ''}
                    onChange={(e) => {
                      const value = e.target.value;
                      setFormData({
                        ...formData,
                        maxDetectionDistanceMiles: value === '' ? null : parseFloat(value)
                      });
                    }}
                    placeholder="Optional (e.g., 10.0)"
                  />
                </div>
              </div>

              <div className="form-row">
                <div className="form-group">
                  <label htmlFor="direction">Direction *</label>
                  <select
                    id="direction"
                    value={formData.direction}
                    onChange={(e) => setFormData({ ...formData, direction: e.target.value as Direction })}
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
                  />
                  Multiple Tracks
                </label>
              </div>

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
