import { useState, useEffect, useMemo } from 'react';
import { AdminBeacon, CreateBeacon, UpdateBeacon } from '../types/AdminBeacon';
import { User } from '../types/User';
import { getBeacons, createBeacon, updateBeacon, deleteBeacon } from '../api/beacons';
import { getUsers } from '../api/users';
import './AdminBeacons.css';
import './AdminSkin.css';
import TextField from '@mui/material/TextField';
import Tooltip from '@mui/material/Tooltip';
import IconButton from '@mui/material/IconButton';
import ClearIcon from '@mui/icons-material/Clear';
import AdminPageHeader from '../components/admin/AdminPageHeader';
import { adminClearButtonSx } from '../components/admin/adminSx';

type SortField = 'name' | 'ownerID' | 'createdAt';
type SortDirection = 'asc' | 'desc' | null;

const AdminBeacons = () => {
  const [beacons, setBeacons] = useState<AdminBeacon[]>([]);
  const [users, setUsers] = useState<User[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [showModal, setShowModal] = useState(false);
  const [editingBeacon, setEditingBeacon] = useState<AdminBeacon | null>(null);
  const [formData, setFormData] = useState<CreateBeacon>({ name: '', ownerID: 0 });
  const [searchTerm, setSearchTerm] = useState('');
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage, setItemsPerPage] = useState(10);
  const [sortField, setSortField] = useState<SortField>('name');
  const [sortDirection, setSortDirection] = useState<SortDirection>('asc');

  useEffect(() => {
    loadData();
  }, []);

  const loadData = async () => {
    setLoading(true);
    
    // Load beacons
    const beaconsResponse = await getBeacons();
    if (beaconsResponse.errors.length > 0) {
      setError(beaconsResponse.errors.join(', '));
    } else if (beaconsResponse.data) {
      setBeacons(beaconsResponse.data);
    }

    // Load users for owner dropdown
    const usersResponse = await getUsers();
    if (usersResponse.data) {
      setUsers(usersResponse.data);
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

  const sortedBeacons = useMemo(() => {
    let sorted = [...beacons];

    if (searchTerm) {
      sorted = sorted.filter(beacon =>
        beacon.name.toLowerCase().includes(searchTerm.toLowerCase())
      );
    }

    if (sortDirection) {
      sorted.sort((a, b) => {
        let aVal: string | number = sortField === 'name' ? a.name : sortField === 'ownerID' ? a.ownerID : a.createdAt;
        let bVal: string | number = sortField === 'name' ? b.name : sortField === 'ownerID' ? b.ownerID : b.createdAt;

        if (sortField === 'createdAt') {
          aVal = new Date(aVal).getTime();
          bVal = new Date(bVal).getTime();
        } else if (sortField === 'name') {
          aVal = (aVal as string).toLowerCase();
          bVal = (bVal as string).toLowerCase();
        }

        if (aVal < bVal) return sortDirection === 'asc' ? -1 : 1;
        if (aVal > bVal) return sortDirection === 'asc' ? 1 : -1;
        return 0;
      });
    }

    return sorted;
  }, [beacons, searchTerm, sortField, sortDirection]);

  const getSortIcon = (field: SortField) => {
    const icon = sortField !== field ? '⇅' : sortDirection === 'asc' ? '⬆' : '⬇';
    return <span style={{ fontSize: '1.2em', marginLeft: '0.3em' }}>{icon}</span>;
  };

  const paginatedBeacons = useMemo(() => {
    const startIndex = (currentPage - 1) * itemsPerPage;
    return sortedBeacons.slice(startIndex, startIndex + itemsPerPage);
  }, [sortedBeacons, currentPage, itemsPerPage]);

  const totalPages = Math.ceil(sortedBeacons.length / itemsPerPage);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchTerm, sortField, sortDirection, itemsPerPage]);

  useEffect(() => {
    if (currentPage > totalPages && totalPages > 0) {
      setCurrentPage(totalPages);
    }
  }, [currentPage, totalPages]);

  const handleAdd = () => {
    setEditingBeacon(null);
    setFormData({ name: '', ownerID: users.length > 0 ? users[0].id : 0 });
    setError(undefined);
    setShowModal(true);
  };

  const handleEdit = (beacon: AdminBeacon) => {
    setEditingBeacon(beacon);
    setFormData({ name: beacon.name, ownerID: beacon.ownerID });
    setError(undefined);
    setShowModal(true);
  };

  const handleDelete = async (id: number) => {
    if (!confirm('Are you sure you want to delete this beacon?')) return;

    const response = await deleteBeacon(id);
    if (response.errors.length > 0) {
      setError(response.errors.join(', '));
    } else {
      setBeacons(beacons.filter(b => b.id !== id));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(undefined);

    if (!formData.name.trim()) {
      setError('Name is required');
      return;
    }

    if (formData.name.length > 100) {
      setError('Name must be 100 characters or less');
      return;
    }

    if (formData.ownerID === 0) {
      setError('Owner is required');
      return;
    }

    if (editingBeacon) {
      const updateData: UpdateBeacon = {
        id: editingBeacon.id,
        name: formData.name,
        ownerID: formData.ownerID,
      };
      const response = await updateBeacon(editingBeacon.id, updateData);
      if (response.errors.length > 0) {
        setError(response.errors.join(', '));
      } else if (response.data) {
        setBeacons(beacons.map(b => b.id === editingBeacon.id ? response.data! : b));
        setShowModal(false);
      }
    } else {
      const response = await createBeacon(formData);
      if (response.errors.length > 0) {
        setError(response.errors.join(', '));
      } else if (response.data) {
        setBeacons([...beacons, response.data]);
        setShowModal(false);
      }
    }
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const year = date.getFullYear();
    return `${month}/${day}/${year}`;
  };

  const getOwnerName = (ownerID: number) => {
    const user = users.find(u => u.id === ownerID);
    return user ? `${user.firstName} ${user.lastName}` : `User ${ownerID}`;
  };

  if (loading) return <div className="admin-loading">Loading...</div>;

  return (
    <div className="admin-beacons admin-page">
      <AdminPageHeader
        title="Beacons"
        description="Manage beacon names, ownership, and linked railroads."
      />

      <div className="admin-controls">
        <div className="search-container">
          <TextField
            label="Filter by Name"
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
          <button className="btn-primary" onClick={handleAdd}>Add Beacon</button>
        </div>
      </div>

      {error && <div className="error-message">{error}</div>}

      <div className="admin-table-container">
        <table className="admin-table">
          <thead>
            <tr>
              <th>Beacon ID</th>
              <th className="sortable" onClick={() => handleSort('name')}>
                Name {getSortIcon('name')}
              </th>
              <th className="sortable" onClick={() => handleSort('ownerID')}>
                Owner {getSortIcon('ownerID')}
              </th>
              <th>Railroads</th>
              <th>Created</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            {paginatedBeacons.map(beacon => (
              <tr key={beacon.id}>
                <td>{beacon.id}</td>
                <td>{beacon.name}</td>
                <td>{getOwnerName(beacon.ownerID)}</td>
                <td>
                  {beacon.beaconRailroads.length > 0 ? (
                    <div className="railroads-list">
                      {beacon.beaconRailroads.map((br, idx) => (
                        <span key={idx} className="railroad-badge">
                          {br.railroadName} - {br.subdivisionName}
                        </span>
                      ))}
                    </div>
                  ) : (
                    <span className="no-data">No railroads</span>
                  )}
                </td>
                <td>{formatDate(beacon.createdAt)}</td>
                <td className="actions-cell">
                  <button className="btn-edit" onClick={() => handleEdit(beacon)}>Edit</button>
                  <button className="btn-delete" onClick={() => handleDelete(beacon.id)}>Delete</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <div className="admin-table-footer-pager">
          <span>Rows per page:</span>
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
            {sortedBeacons.length === 0
              ? '0-0 of 0'
              : `${((currentPage - 1) * itemsPerPage) + 1}-${Math.min(currentPage * itemsPerPage, sortedBeacons.length)} of ${sortedBeacons.length}`}
          </span>
          <button
            type="button"
            className="admin-table-footer-btn"
            onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
            disabled={currentPage === 1 || sortedBeacons.length === 0}
            aria-label="Go to previous page"
          >
            ‹
          </button>
          <button
            type="button"
            className="admin-table-footer-btn"
            onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
            disabled={currentPage === totalPages || sortedBeacons.length === 0}
            aria-label="Go to next page"
          >
            ›
          </button>
        </div>
      </div>

      {showModal && (
        <div className="modal-overlay" onClick={() => setShowModal(false)}>
          <div className="modal-content" onClick={(e) => e.stopPropagation()}>
            <h2>{editingBeacon ? 'Edit Beacon' : 'Add Beacon'}</h2>
            <form onSubmit={handleSubmit}>
              {error && <div className="error-message">{error}</div>}
              
              <div className="form-group">
                <label htmlFor="name">Name *</label>
                <input
                  id="name"
                  type="text"
                  value={formData.name}
                  onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                  maxLength={100}
                  placeholder="Enter beacon name"
                />
              </div>

              <div className="form-group">
                <label htmlFor="ownerID">Owner *</label>
                <select
                  id="ownerID"
                  value={formData.ownerID}
                  onChange={(e) => setFormData({ ...formData, ownerID: parseInt(e.target.value) })}
                >
                  <option value={0}>Select an owner</option>
                  {users.map(user => (
                    <option key={user.id} value={user.id}>
                      {user.firstName} {user.lastName} ({user.email})
                    </option>
                  ))}
                </select>
              </div>

              <div className="form-actions">
                <button type="button" className="btn-secondary" onClick={() => setShowModal(false)}>
                  Cancel
                </button>
                <button type="submit" className="btn-primary">
                  {editingBeacon ? 'Update' : 'Create'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
};

export default AdminBeacons;
